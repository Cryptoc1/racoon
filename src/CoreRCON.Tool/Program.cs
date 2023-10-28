using System.Net;
using System.Net.Sockets;
using CoreRCON;
using Spectre.Console;
using Spectre.Console.Cli;

using RCONStatus = CoreRCON.Parsers.Standard.Status;

var app = new CommandApp<ShellCommand>();
app.Configure(options =>
{
    options.SetApplicationName("rcon-repl");
    options.PropagateExceptions();
});

return await app.RunAsync(args);

internal sealed class ShellCommand : AsyncCommand<ShellParameters>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ShellParameters parameters)
    {
        var host = await ResolveHost(parameters.Host);
        if (host is null)
        {
            AnsiConsole.Write($"[bold red]{NerdFontIcon.SearchWeb}[/] Provided host could not be resolved.");
            return ShellExitCode.InvalidHost;
        }

        var password = string.IsNullOrWhiteSpace(parameters.Password)
            ? AnsiConsole.Prompt(new TextPrompt<string>($"Password{NerdFontIcon.AngleRight}").Secret())
            : parameters.Password;

        using var console = new RCON(host, parameters.Port, password, new(parameters.Timeout, parameters.IsMultiPacketSupported));
        if (!await TryConnect(console))
        {
            return ShellExitCode.FailedToConnect;
        }

        bool error = false;
        var status = await console.SendCommandAsync<RCONStatus>("status");

        while (console.ConnectionState is RCONConnectionState.Authenticated)
        {
            var command = AnsiConsole.Ask<string>($"[bold {(error ? "orange" : "green")}]{NerdFontIcon.LanConnect}[/]{status.Hostname} [bold]{NerdFontIcon.AngleRight}[/]").Trim();
            error = false;

            if (string.IsNullOrWhiteSpace(command))
            {
                continue;
            }

            if (command[0] is not ':')
            {
                try
                {
                    var result = await console.SendCommandAsync(command);
                    AnsiConsole.WriteLine(result);
                }
                catch (RCONCommandException exception)
                {
                    AnsiConsole.WriteException(exception);
                    error = true;
                }

                continue;
            }

            if (command.Equals(":clear", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.Clear();
                continue;
            }

            if (command.Equals(":q", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
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

    private static async Task<bool> TryConnect(RCON console)
    {
        try
        {
            await AnsiConsole.Status()
                .StartAsync("Connecting...", context =>
                {
                    context.Spinner(Spinner.Known.Dots3);
                    return console.ConnectAsync();
                });
        }
        catch (Exception exception)
        when (exception is RCONException or SocketException)
        {
            return AnsiConsole.Confirm($"[bold red]{NerdFontIcon.LanDisconnect}[/]Failed to connect to the host, retry?")
                && await TryConnect(console);
        }

        return true;
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

    [CommandOption("--multi-packet")]
    public bool IsMultiPacketSupported { get; init; }

    [CommandArgument(1, "[password]")]
    public string Password { get; init; } = default!;

    [CommandOption("-p|--port")]
    public ushort Port { get; init; } = 27015;

    [CommandOption("-t|--timeout")]
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
}
