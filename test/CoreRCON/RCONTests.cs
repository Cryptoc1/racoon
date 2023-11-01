using System.Configuration.Assemblies;
using System.Net;
using System.Net.Sockets;

namespace CoreRCON.Tests;

public sealed class RCONTests
{
    [Fact]
    public async Task ConnectAsync_Throws_RCONException_WhenSocketFailsToConnect()
    {
        using var console = new RCON(IPAddress.None, 0, string.Empty, new(TimeSpan.Zero));

        var exception = await Assert.ThrowsAsync<RCONException>(() => console.ConnectAsync());
        Assert.StartsWith("An attempt to connect to with the host failed.", exception.Message);
        Assert.IsType<SocketException>(exception.InnerException);
    }

    [Fact]
    public void Dispose_DoesNotThrow_WhenNotConnected()
    {
        var console = new RCON(IPAddress.None, 0, string.Empty);

        // NOTE: implicit failure if an exception is thrown
        console.Dispose();
    }

    [Fact]
    public async Task SendCommandAsync_Throws_RCONException_WhenNotConnected()
    {
        var rcon = new RCON(IPAddress.None, 0, string.Empty);

        var exception = await Assert.ThrowsAsync<RCONException>(() => rcon.SendCommandAsync("status"));
        Assert.StartsWith("The connection has not been created.", exception.Message);
    }
}
