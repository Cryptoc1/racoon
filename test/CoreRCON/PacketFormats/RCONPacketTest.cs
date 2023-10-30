using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using CoreRCON.PacketFormats;
using Microsoft.VisualBasic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CoreRCON.Tests.PacketFormats;

[TestClass]
public sealed class RCONPacketTests
{
    [TestMethod]
    public void GetBytes_Throws_BufferTooSmall()
    {
        var packet = new RCONPacket(0, RCONPacketType.Response, string.Empty);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => packet.GetBytes(new byte[packet.Size]));
    }

    [TestMethod]
    [DynamicData(nameof(TestData))]
    public void GetBytes_Writes_BytesToBuffer(RCONPacket packet, byte[] bytes)
    {
        var buffer = new byte[packet.Size + sizeof(int)];
        packet.GetBytes(buffer);

        CollectionAssert.AreEqual(bytes, buffer);
    }

    [TestMethod]
    [DynamicData(nameof(TestData))]
    public void TryFromBytes_Converts_Bytes(RCONPacket packet, byte[] bytes)
    {
        if (!RCONPacket.TryFromBytes(bytes, out var p))
        {
            Assert.Fail("Failed to convert bytes to packet.");
        }

        Assert.AreEqual(packet, p);
    }

    [TestMethod]
    [GenerateBytes(10, RandomizeLength = true)]
    public void TryFromBytes_DoesNotThrow_DataNotValid(byte[] bytes)
    {
        // NOTE: implicit failure if an exception is thrown
        RCONPacket.TryFromBytes(bytes, out var packet);
    }

    public static IEnumerable<object[]> TestData => [
        [new RCONPacket(0, RCONPacketType.Auth, string.Empty), new byte[] {10, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0, 0}],
        [new RCONPacket(1, RCONPacketType.Response, "Hello, World!"), new byte[] { 23, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 72, 101, 108, 108, 111, 44, 32, 87, 111, 114, 108, 100, 33, 0, 0 }]
    ];
}
