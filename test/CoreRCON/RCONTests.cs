using System.Net;

namespace CoreRCON.Tests;

public sealed class RCONTests
{
    [Fact]
    public void Dispose_DoesNotThrow_WhenNotConnected()
    {
        var rcon = new RCON(IPAddress.Loopback, 0, string.Empty);

        // NOTE: implicit failure if an exception is thrown
        rcon.Dispose();
    }

    [Fact]
    public async Task SendCommandAsync_Throws_WhenNotConnected()
    {
        var rcon = new RCON(IPAddress.Loopback, 0, string.Empty);

        var exception = await Assert.ThrowsAsync<RCONException>(() => rcon.SendCommandAsync("status"));
        Assert.StartsWith("The connection has not been created.", exception.Message);
    }
}
