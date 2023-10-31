using System.Runtime.CompilerServices;
using System.Text;
using CoreRCON.PacketFormats;

namespace CoreRCON.Tests.PacketFormats;

public sealed class LogAddressPacketTests
{
    [Theory]
    [GenerateBytes(10)]
    public void TryFromBytes_DoesNotThrow_When_DataNotValid(byte[] bytes)
    {
        // NOTE: implicit failure if an exception is thrown
        LogAddressPacket.TryFromBytes(bytes, out var _);
    }

    [Theory]
    [InlineData(@"L 10/19/2023 - 20:15:34: Started """"")]
    [InlineData(@"10/19/2023 - 20:15:34.000 - Started """"")]
    public void TryFromBytes_Converts_HLStandardAndHttp(string value)
    {
        var bytes = new byte[value.Length + 5];
        bytes[0] = 0xFF;
        bytes[1] = 0xFF;
        bytes[2] = 0xFF;
        bytes[3] = 0xFF;
        bytes[4] = 83;

        Encoding.UTF8.GetBytes(value, 0, value.Length, bytes, 5);

        var converted = LogAddressPacket.TryFromBytes(bytes, out var _);
        Assert.True(converted);
    }
}
