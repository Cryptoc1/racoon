using System.Net;
using System.Text;
using CoreRCON;
using Spectre.Console;
using Spectre.Console.Cli;

using RCONStatus = CoreRCON.Parsers.Standard.Status;

var app = new CommandApp<ShellCommand>();
app.Configure(options =>
{
    options.SetApplicationName("CoreRCON.Tool");
    options.PropagateExceptions();
});

return await app.RunAsync(args);

internal sealed class ShellCommand : AsyncCommand<ShellParameters>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ShellParameters parameters)
    {
        if (!parameters.NoLogo)
        {
            AnsiConsole.Write(
                new FigletText("CoreRCON.Tool").Color(Color.MediumSpringGreen));
        }

        var host = await ResolveHost(parameters.Host);
        if (host is null)
        {
            AnsiConsole.Write($"[bold red]{NerdFontIcon.SearchWeb}[/] Provided host could not be resolved.");
            return ShellExitCode.InvalidHost;
        }

        var password = string.IsNullOrWhiteSpace(parameters.Password)
            ? AnsiConsole.Prompt(new TextPrompt<string>($"Password{NerdFontIcon.AngleRight}").Secret())
            : parameters.Password;

        using var console = new RCON(host, parameters.Port, password, new RCONOptions(parameters.Timeout, parameters.UseKoraktorMethod));
        if (!await TryConnect(console))
        {
            return ShellExitCode.FailedToConnect;
        }

        using var cancellation = new CancellationTokenSource();
        console.Disconnected += cancellation.Cancel;

        var prompt = new RCONPrompt(
            await console.SendCommandAsync<RCONStatus>("status"));

        while (console.ConnectionState is RCONConnectionState.Authenticated)
        {
            var command = await prompt.ShowAsync(AnsiConsole.Console, cancellation.Token);
            if (command[0] is not ':')
            {
                try
                {
                    var result = await console.SendCommandAsync(command);
                    AnsiConsole.WriteLine(result);

                    prompt.Error = result.StartsWith("unknown command", StringComparison.OrdinalIgnoreCase);
                }
                catch (RCONCommandException exception)
                {
                    AnsiConsole.WriteException(exception);
                    prompt.Error = true;
                }
            }

            if (command.Equals(":clear", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.Clear();
            }

            if (command.Equals(":q", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            prompt.AddHistory(command);
        }

        return ShellExitCode.Disconnected;
    }

    private static async Task<IPAddress?> ResolveHost(string host)
    {
        if (IPAddress.TryParse(host, out var address))
        {
            return address;
        }

        var addresses = await Dns.GetHostAddressesAsync(host);
        if (addresses.Length is 1)
        {
            return addresses[0];
        }
        else if (addresses.Length > 1)
        {
            return AnsiConsole.Prompt(
                new SelectionPrompt<IPAddress>()
                    .Title($"Host [green]{host}[/] resolved to multiple addresses {NerdFontIcon.AngleRight}")
                    .AddChoices(addresses));
        }

        return default;
    }

    private static async Task<bool> TryConnect(RCON console, int retry = default)
    {
        try
        {
            await AnsiConsole.Status()
                .StartAsync("Connecting...", async context =>
                {
                    context.Spinner(Spinner.Known.Dots3);
                    await Task.Delay(retry is 0 ? 250 : 1250);

                    await console.ConnectAsync();
                });
        }
        catch (RCONException)
        {
            return AnsiConsole.Confirm($"[bold red]{NerdFontIcon.LanDisconnect}[/]Failed to connect to the host, retry?")
                && await TryConnect(console, ++retry);
        }

        return true;
    }
}

internal sealed class RCONPrompt(RCONStatus status, int capacity = 1024) : IPrompt<string>
{
    public bool Error { get; set; }

    // private readonly RCON _console = console;
    private readonly List<string> _history = new(capacity);

    public string Show(IAnsiConsole console) => ShowAsync(console, CancellationToken.None).GetAwaiter().GetResult();
    public async Task<string> ShowAsync(IAnsiConsole console, CancellationToken cancellation)
    {
        _ = WritePrompt(console);
        var value = await console.RunExclusive(async () =>
        {
            var position = _history.Count;
            var text = new StringBuilder();
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();

                var key = await console.Input.ReadKeyAsync(true, cancellation);
                if (!key.HasValue)
                {
                    continue;
                }

                if (key.Value.Key is ConsoleKey.Enter)
                {
                    if (text.Length is 0)
                    {
                        continue;
                    }

                    console.WriteLine();
                    return text.ToString();
                }

                if (_history.Count is not 0 && key.Value.Key is ConsoleKey.UpArrow)
                {
                    var value = _history[position = Math.Max(--position, 0)];
                    console.Write(string.Concat(
                        Enumerable.Range(0, text.Length)
                            .Select(_ => "\b \b")));

                    text = text.Clear().Append(value);
                    console.Write(value);

                    continue;
                }

                if (_history.Count is not 0 && key.Value.Key is ConsoleKey.DownArrow)
                {
                    var value = (position = Math.Min(++position, _history.Count)) == _history.Count
                        ? string.Empty
                        : _history[position];

                    console.Write(string.Concat(
                        Enumerable.Range(0, text.Length)
                            .Select(_ => "\b \b")));

                    text = text.Clear().Append(value);
                    console.Write(value);
                }

                if (key.Value.Key is ConsoleKey.Backspace)
                {
                    if (text.Length > 0)
                    {
                        text = text.Remove(text.Length - 1, 1);
                        console.Write("\b \b");
                    }

                    continue;
                }

                var character = key.Value.KeyChar;
                if (!char.IsControl(character))
                {
                    text = text.Append(character);
                    console.Write(character.ToString());

                    continue;
                }
            }
        });

        if (string.IsNullOrWhiteSpace(value))
        {
            return await ShowAsync(console, cancellation);
        }

        return value.Trim();
    }

    public void AddHistory(string command)
    {
        if (_history.Count == _history.Capacity)
        {
            _history.RemoveAt(0);
        }

        _history.Add(command);
    }

    private int WritePrompt(IAnsiConsole console)
    {
        var markup = new Markup($"[bold {(Error ? "orange1" : Color.MediumSpringGreen)}]{NerdFontIcon.LanConnect}[/]{status.Hostname} [bold]{NerdFontIcon.AngleRight}[/] ");
        console.Write(markup);

        return markup.Length;
    }
}

internal static class NerdFontIcon
{
    public const string AngleRight = "\uf105";
    public const string LanConnect = "\udb80\udf18";
    public const string LanDisconnect = "\udb80\udf19";
    public const string SearchWeb = "\udb81\udf0f";
}

internal static class ShellExitCode
{
    public const int Disconnected = -50;
    public const int FailedToConnect = -100;
    public const int InvalidHost = -75;
}

internal sealed class ShellParameters : CommandSettings
{
    [CommandArgument(0, "<host>")]
    public string Host { get; init; } = default!;

    [CommandOption("--no-logo")]
    public bool NoLogo { get; init; } = Environment.GetEnvironmentVariable("CORERCON_NOLOGO") == bool.TrueString;

    [CommandArgument(1, "[password]")]
    public string Password { get; init; } = string.Empty;

    [CommandOption("-p|--port")]
    public ushort Port { get; init; } = 27015;

    [CommandOption("-t|--timeout")]
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    [CommandOption("--use-koraktor")]
    public bool UseKoraktorMethod { get; init; } = false;
}
