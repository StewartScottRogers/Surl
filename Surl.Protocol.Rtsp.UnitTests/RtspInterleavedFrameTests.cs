namespace Surl.Protocol.Rtsp;

[TestClass]
public sealed class RtspInterleavedFrameTests
{
    [TestMethod]
    [DataRow(new byte[] { 0x80, 96, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, DisplayName = "shorter than the header")]
    [DataRow(new byte[] { 0x40, 96, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 }, DisplayName = "version 1")]
    [DataRow(new byte[] { 0xC0, 96, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 }, DisplayName = "version 3")]
    [DataRow(new byte[] { 0x81, 96, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 }, DisplayName = "a CSRC past the end")]
    [DataRow(new byte[] { 0x90, 96, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2 }, DisplayName = "an extension header past the end")]
    [DataRow(new byte[] { 0x90, 96, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 0, 1 }, DisplayName = "an extension past the end")]
    [DataRow(new byte[] { 0xA0, 96, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 3 }, DisplayName = "padding past the payload")]
    public void RtpPayload_PacketTooShortOrNotVersion2_IsNone(byte[] packet)
    {
        Assert.IsNull(RtspInterleavedFrame.RtpPayload(packet));
    }

    [TestMethod]
    public void RtpPayload_WithACsrcAnExtensionAndPadding_IsTheBytesBetweenThem()
    {
        byte[] packet =
        [
            0xB1, 96, 0, 1, 0, 0, 0, 2, 0, 0, 0, 3,
            0, 0, 0, 4,
            0xBE, 0xDE, 0, 1, 0xEE, 0xEE, 0xEE, 0xEE,
            (byte)'x', (byte)'y', (byte)'z',
            0, 2,
        ];

        var payload = RtspInterleavedFrame.RtpPayload(packet);

        CollectionAssert.AreEqual("xyz"u8.ToArray(), packet[payload!.Value]);
    }
}
