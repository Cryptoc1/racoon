using System.Net;
using System.Net.Sockets;
using CoreRCON.PacketFormats;
using CoreRCON.Parsers;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON;

public sealed class LogReceiver : IDisposable
{
    public int ResolvedPort => ((IPEndPoint)_udp.Client.LocalEndPoint).Port;

    private readonly CancellationTokenSource _cancellation = new();
    private readonly IPEndPoint[] _sources;
    private readonly UdpClient _udp;

    public event Action<LogAddressPacket>? PacketReceived;

    /// <summary> Opens a socket to receive LogAddress logs, and registers it with the server.  The IP can also be a local IP if the server is on the same network. </summary>
    /// <param name="port"> Local port to bind to. </param>
    /// <param name="sources"> Array of endpoints to accept connections from. </param>
    public LogReceiver(ushort port, params IPEndPoint[] sources)
    {
        _sources = sources;
        _udp = new(port);

        // start reading from the socket
        Task.Run(Receive);
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _cancellation.Dispose();

        _udp.Dispose();
    }

    /// <summary> Listens on the socket for messages of type <typeparamref name="T"/>. </summary>
    public IDisposable Listen<T>(Action<T> listener)
        where T : class, IParseable<T>
        => Listen((LogAddressPacket packet) =>
        {
            if (packet.Body.Length is 0)
            {
                return;
            }

            var parser = ParserPool.Shared.Get<T>();
            if (!parser.IsMatch(packet.Body))
            {
                return;
            }

            var value = parser.Parse(packet.Body);
            listener(value);
        });

    /// <summary> Listens on the socket for anything from LogAddress, returning the full packet. </summary>
    public IDisposable Listen(Action<LogAddressPacket> listener) => new LogListener(this, listener);

    /// <summary> Listens on the socket for anything, returning just the body of the packet. </summary>
    public IDisposable Listen(Action<string> listener) => Listen(packet =>
    {
        if (packet.Body.Length is 0)
        {
            return;
        }

        listener(packet.Body);
    });

    private async Task Receive()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            var result = await _udp.ReceiveAsync();
            if (!_sources.Contains(result.RemoteEndPoint))
            {
                // ignore unauthorized packet
                return;
            }

            if (LogAddressPacket.TryFromBytes(result.Buffer, out var packet))
            {
                PacketReceived?.Invoke(packet);
            }
        }
    }

    private sealed class LogListener : IDisposable
    {
        private readonly LogReceiver receiver;
        private readonly Action<LogAddressPacket> listener;

        public LogListener(LogReceiver receiver, Action<LogAddressPacket> listener)
        {
            this.receiver = receiver;
            this.listener = listener;
            receiver.PacketReceived += listener;
        }

        public void Dispose() => receiver.PacketReceived -= listener;
    }
}
