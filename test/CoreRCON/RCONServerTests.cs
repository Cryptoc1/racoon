using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

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

    [Fact(DisplayName = "ListenAsync: does not throw when server is disposed")]
    public async Task ListenAsync_DoesNotThrow_WhenServerIsDisposed()
    {
        using var accessor = await RCONServerAccessor.Acquire();
        var listening = accessor.Server.ListenAsync();

        accessor.Server.Dispose();

        // NOTE: implicit failure if task throws
        await listening;
    }

    [Fact(DisplayName = "ListenAsync: completes when cancelled")]
    public async Task ListenAsync_Completes_WhenCancelled()
    {
        using var accessor = await RCONServerAccessor.Acquire();
        using var cancellation = new CancellationTokenSource();

        var listening = accessor.Server.ListenAsync(cancellation.Token);
        cancellation.Cancel();

        // NOTE: implicit failure if task throws
        await listening;

        Assert.False(listening.IsCanceled);
        Assert.Equal(TaskStatus.RanToCompletion, listening.Status);
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

