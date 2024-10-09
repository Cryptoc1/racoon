using System.Net;

namespace CoreRCON.Tests;

[Collection("RCONConnectionTests")]
public sealed class RCONServerTests
{
    [Fact(DisplayName = "Connection: raised when client connects")]
    public async Task Connection_IsRaised_WhenClientConnects()
    {
        using var accessor = await RCONServerAccessor.Acquire();
        _ = accessor.Server.ListenAsync();

        var raised = await Assert.RaisesAsync<ConnectionEventArgs>(
            handler => accessor.Server.Connection += handler,
            handler => accessor.Server.Connection -= handler,
            async () =>
            {
                using var client = new RCONClient(new(IPAddress.Loopback, 27015), "TEST");
                await client.ConnectAsync();
            });

        Assert.NotNull(raised.Arguments.Connection);
    }

    [Fact(DisplayName = "ListenAsync: throws when server is disposed")]
    public async Task ListenAsync_Throws_WhenServerDisposed()
    {
        using var accessor = await RCONServerAccessor.Acquire();
        var listening = accessor.Server.ListenAsync();

        accessor.Server.Dispose();
        await Assert.ThrowsAsync<OperationCanceledException>(() => listening);
    }

    [Fact(DisplayName = "ListenAsync: throws when cancelled")]
    public async Task ListenAsync_Throws_WhenCancelled()
    {
        using var accessor = await RCONServerAccessor.Acquire();
        using var cancellation = new CancellationTokenSource();

        var listening = accessor.Server.ListenAsync(cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => listening);
    }

    [Fact(DisplayName = "PacketReceived: raised when client sends packet")]
    public async Task PacketReceived_IsRaised_WhenClientSendsPacket()
    {
        using var accessor = await RCONServerAccessor.Acquire();
        _ = accessor.Server.ListenAsync();

        var raised = await Assert.RaisesAsync<PacketReceivedEventArgs>(
            handler => accessor.Server.PacketReceived += handler,
            handler => accessor.Server.PacketReceived -= handler,
            async () =>
            {
                using var client = new RCONClient(new(IPAddress.Loopback, 27015), "TEST");
                await client.ConnectAsync();
            });

        Assert.Equal(0, raised.Arguments.Packet.Id);
    }
}

