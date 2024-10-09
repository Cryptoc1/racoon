namespace CoreRCON.Tests;

public sealed class RCONPacketTests
{
    [Fact(DisplayName = "GetBytes: throws when buffer is too small")]
    public void GetBytes_Throws_When_BufferTooSmall()
    {
        var packet = new RCONPacket(0, RCONPacketType.Response, string.Empty);
        Assert.Throws<ArgumentOutOfRangeException>(() => packet.GetBytes(new byte[packet.Size]));
    }

    [Theory(DisplayName = "GetBytes: writes to buffer")]
    [MemberData(nameof(TestData))]
    public void GetBytes_Writes_ToBuffer(RCONPacket packet, byte[] bytes)
    {
        var buffer = new byte[packet.Size + sizeof(int)];
        packet.GetBytes(buffer);

        Assert.Equal(bytes, buffer);
    }

    [Theory(DisplayName = "TryFromBytes: converts")]
    [MemberData(nameof(TestData))]
    public void TryFromBytes_Converts_Bytes(RCONPacket packet, byte[] bytes)
    {
        if (!RCONPacket.TryFromBytes(bytes, out var p))
        {
            Assert.Fail("Failed to convert bytes to packet.");
        }

        Assert.Equal(packet, p);
    }

    [Theory(DisplayName = "TryFromBytes: does not throw when data not valid")]
    [GenerateBytes(5, RandomizeLength = true)]
    public void TryFromBytes_DoesNotThrow_When_DataNotValid(byte[] bytes)
    {
        // NOTE: implicit failure if an exception is thrown
        RCONPacket.TryFromBytes(bytes, out var packet);
    }

    public static IEnumerable<object[]> TestData => [
        [new RCONPacket(0, RCONPacketType.Auth, string.Empty), new byte[] { 10, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0, 0 }],
        [new RCONPacket(1, RCONPacketType.Response, "Hello, World!"), new byte[] { 23, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 72, 101, 108, 108, 111, 44, 32, 87, 111, 114, 108, 100, 33, 0, 0 }]
    ];
}
