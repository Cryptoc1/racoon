using System.Runtime.CompilerServices;
using System.Text;
using CoreRCON.PacketFormats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CoreRCON.Tests.PacketFormats;

[TestClass]
public sealed class LogAddressPacketTests
{
    [TestMethod]
    [DataRow(@"L 10/19/2023 - 20:15:34: Started """"")]
    [DataRow(@"10/19/2023 - 20:15:34.000 - Started """"")]
    public void TryFromBytes_Supports_HLStandard_And_Http_Formats(string value)
    {
        var bytes = new byte[value.Length + 5];
        bytes[0] = 0xFF;
        bytes[1] = 0xFF;
        bytes[2] = 0xFF;
        bytes[3] = 0xFF;
        bytes[4] = 83;

        Encoding.UTF8.GetBytes(value, 0, value.Length, bytes, 5);

        var converted = LogAddressPacket.TryFromBytes(bytes, out var packet);
        Assert.IsTrue(converted);
    }
}
