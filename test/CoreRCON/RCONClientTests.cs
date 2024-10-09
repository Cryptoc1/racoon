using System.Net;
using System.Net.Sockets;

namespace CoreRCON.Tests;

[Collection("RCONConnectionTests")]
public sealed class RCONClientTests
{
    [Fact(DisplayName = "ConnectAsync: connects")]
    public async Task ConnectAsync_Connects()
    {
        using var accessor = await RCONServerAccessor.Acquire();
        _ = accessor.Server.ListenAsync();

        using var client = new RCONClient(IPAddress.Loopback, 27015, "TEST", new(TimeSpan.Zero));
        await client.ConnectAsync();
    }

    [Fact(DisplayName = "ConnectAsync: throws when socket fails to connect")]
    public async Task ConnectAsync_Throws_RCONException_WhenSocketFailsToConnect()
    {
        using var client = new RCONClient(IPAddress.None, 0, string.Empty);

        var exception = await Assert.ThrowsAsync<RCONException>(client.ConnectAsync);
        Assert.StartsWith("An attempt to connect to with the host failed.", exception.Message);
        Assert.IsType<SocketException>(exception.InnerException);
    }

    [Fact(DisplayName = "ConnectAsync: throws on invalid password")]
    public async Task ConnectAsync_Throws_RCONAuthenticationException_WhenPasswordIsIncorrect()
    {
        using var accessor = await RCONServerAccessor.Acquire();
        _ = accessor.Server.ListenAsync();

        using var client = new RCONClient(new(IPAddress.Loopback, 27015), "TESTING", new(TimeSpan.Zero));
        await Assert.ThrowsAsync<RCONAuthenticationException>(client.ConnectAsync);
    }

    [Fact(DisplayName = "ConnectAsync: throws on timeout")]
    public async Task ConnectAsync_Throws_RCONException_OnTimeout()
    {
        using var accessor = await RCONServerAccessor.Acquire();
        accessor.Server.PacketReceived += async (_, e) =>
        {
            e.Handled = true;
            await Task.Delay(
                TimeSpan.FromSeconds(1));
        };

        _ = accessor.Server.ListenAsync();
        using var client = new RCONClient(
            IPAddress.Loopback,
            27015,
            string.Empty,
            new(TimeSpan.FromMilliseconds(500)));

        var exception = await Assert.ThrowsAsync<RCONException>(client.ConnectAsync);
        Assert.IsType<TaskCanceledException>(exception.InnerException);
    }

    [Fact(DisplayName = "Dispose: does not throw when not connected")]
    public void Dispose_DoesNotThrow_WhenNotConnected()
    {
        var client = new RCONClient(IPAddress.None, 0, string.Empty);

        // NOTE: implicit failure if an exception is thrown
        client.Dispose();
    }

    [Fact(DisplayName = "PacketReceived: raised when packet is received")]
    public async Task PacetReceived_IsRaised_WhenPacketIsReceived()
    {
        using var accessor = await RCONServerAccessor.Acquire();
        _ = accessor.Server.ListenAsync();

        using var client = new RCONClient(new(IPAddress.Loopback, 27015), "TEST");
        var raised = await Assert.RaisesAsync<PacketReceivedEventArgs>(
            handler => client.PacketReceived += handler,
            handler => client.PacketReceived -= handler,
            client.ConnectAsync);

        Assert.Equal(0, raised.Arguments.Packet.Id);
    }

    [Fact(DisplayName = "SendCommandAsync: throws when not connected")]
    public async Task SendCommandAsync_Throws_RCONCommandException_WhenNotConnected()
    {
        using var client = new RCONClient(IPAddress.None, 0, string.Empty);
        var exception = await Assert.ThrowsAsync<RCONCommandException>(
            async () => await client.SendCommandAsync("status"));

        Assert.StartsWith("The connection has not been", exception.Message);
    }

    [Fact(DisplayName = "SendCommandAsync: sends command")]
    public async Task SendCommandAsync_SendsCommand()
    {
        using var accessor = await RCONServerAccessor.Acquire();
        accessor.Server.PacketReceived += async (_, e) =>
        {
            if (e.Packet.Type is RCONPacketType.ExecCommand)
            {
                e.Handled = true;

                // echo
                await e.Connection.SendAsync(new(e.Packet.Id, RCONPacketType.Response, e.Packet.Body));
            }
        };

        _ = accessor.Server.ListenAsync();

        using var client = new RCONClient(new(IPAddress.Loopback, 27015), "TEST");
        await client.ConnectAsync();

        var result = await client.SendCommandAsync("TEST");
        Assert.Equal("TEST", result);
    }
}
