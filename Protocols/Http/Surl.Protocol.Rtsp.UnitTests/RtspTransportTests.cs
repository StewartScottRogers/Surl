namespace Surl.Protocol.Rtsp;

[TestClass]
public sealed class RtspTransportTests
{
    private const string BadChannels = "the interleaved channels are not an even channel and the one after it, both below 256";

    [TestMethod]
    [DataRow("RTP/AVP/TCP", 0, false)]
    [DataRow("rtp/avp/tcp;unicast;interleaved=2-3", 2, false)]
    [DataRow("RTP/AVP/TCP;interleaved=254-255", 254, false)]
    [DataRow("RTP/AVP/TCP;mode=play", 0, false)]
    [DataRow("RTP/AVP/TCP;mode=\"PLAY\"", 0, false)]
    [DataRow("RTP/AVP/TCP;mode=Record", 0, true)]
    [DataRow("RTP/AVP;unicast;client_port=5000-5001, RTP/AVP/TCP;multicast, RTP/AVP/TCP;interleaved=6-7", 6, false)]
    public void Choose_AnAcceptedAlternative_TakesTheFirstUnicastInterleavedTcpOne(string transport, int rtpChannel, bool records)
    {
        var chosen = RtspTransport.Choose([transport], out var whyUnsupported);

        Assert.AreEqual(new RtspTransport((byte)rtpChannel, records), chosen);
        Assert.AreEqual(rtpChannel + 1, chosen!.RtcpChannel);
        Assert.IsNull(whyUnsupported);
    }

    [TestMethod]
    public void Choose_SeveralTransportFields_ReadsThemInOrder()
    {
        var chosen = RtspTransport.Choose(["RTP/AVP;unicast", "RTP/AVP/TCP;interleaved=8-9"], out _);

        Assert.AreEqual(8, chosen!.RtpChannel);
    }

    [TestMethod]
    [DataRow("RTP/AVP;unicast;client_port=5000-5001", "no RTP/AVP/TCP alternative")]
    [DataRow("RTP/AVP/UDP", "no RTP/AVP/TCP alternative")]
    [DataRow("RTP/AVP/TCP;MULTICAST", "no RTP/AVP/TCP alternative")]
    [DataRow("RTP/AVP/TCP;interleaved=0", BadChannels)]
    [DataRow("RTP/AVP/TCP;interleaved=0-1-2", BadChannels)]
    [DataRow("RTP/AVP/TCP;interleaved=x-1", BadChannels)]
    [DataRow("RTP/AVP/TCP;interleaved=0-x", BadChannels)]
    [DataRow("RTP/AVP/TCP;interleaved=1-2", BadChannels)]
    [DataRow("RTP/AVP/TCP;interleaved=0-2", BadChannels)]
    [DataRow("RTP/AVP/TCP;interleaved=255-256", BadChannels)]
    [DataRow("RTP/AVP/TCP;mode=receive", "the mode is neither play nor record")]
    public void Choose_NoAcceptableAlternative_IsNullWithWhy(string transport, string why)
    {
        var chosen = RtspTransport.Choose([transport], out var whyUnsupported);

        Assert.IsNull(chosen);
        Assert.AreEqual(why, whyUnsupported);
    }

    [TestMethod]
    public void Describe_NamesTheChannelsAndTheSsrcInUpperCaseHex()
    {
        Assert.AreEqual("RTP/AVP/TCP;unicast;interleaved=4-5;ssrc=00ABCDEF", new RtspTransport(4, false).Describe(0x00AB_CDEF));
    }
}
