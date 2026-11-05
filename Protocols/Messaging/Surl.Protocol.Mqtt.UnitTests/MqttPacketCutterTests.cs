namespace Surl.Protocol.Mqtt;

[TestClass]
public sealed class MqttPacketCutterTests
{
    [TestMethod]
    public void Cut_TwoPacketsInOneChunk_CutsBoth()
    {
        var cutter = new MqttPacketCutter();

        var packets = cutter.Cut([0x20, 0x02, 0x00, 0x00, 0xE0, 0x00]);

        Assert.HasCount(2, packets);
        CollectionAssert.AreEqual(new byte[] { 0x20, 0x02, 0x00, 0x00 }, packets[0].Packet);
        Assert.AreEqual(2, packets[0].HeaderLength);
        CollectionAssert.AreEqual(new byte[] { 0xE0, 0x00 }, packets[1].Packet);
    }

    [TestMethod]
    public void Cut_APacketAcrossChunksWithATwoByteRemainingLength_CutsItWhenWhole()
    {
        var cutter = new MqttPacketCutter();
        byte[] packet = [0x30, 0x80, 0x01, .. new byte[128]];

        var first = cutter.Cut(packet.AsSpan(0, 2));
        var second = cutter.Cut(packet.AsSpan(2, 100));
        var third = cutter.Cut(packet.AsSpan(102));

        Assert.IsEmpty(first);
        Assert.IsEmpty(second);
        Assert.HasCount(1, third);
        Assert.AreEqual(3, third[0].HeaderLength);
        CollectionAssert.AreEqual(packet, third[0].Packet);
    }

    [TestMethod]
    public void Cut_ARemainingLengthPastFourBytes_CutsNothingMore()
    {
        var cutter = new MqttPacketCutter();

        var malformed = cutter.Cut([0x30, 0xFF, 0xFF, 0xFF, 0xFF, 0x01]);
        var after = cutter.Cut([0xE0, 0x00]);

        Assert.IsEmpty(malformed);
        Assert.IsEmpty(after);
    }
}
