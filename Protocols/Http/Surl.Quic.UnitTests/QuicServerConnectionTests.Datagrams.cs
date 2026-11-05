using System.Security.Cryptography;

namespace Surl.Quic;

/// <summary>
/// <c>DATAGRAM</c> frames on a connection (BL-573, ADR-0086 decision 4). Every byte is written
/// from RFC 9221: <c>max_datagram_frame_size</c> is transport parameter 0x20 (section 3), and the
/// frame is type 0x30 running to the end of its packet or 0x31 with a Length (section 4).
/// </summary>
public sealed partial class QuicServerConnectionTests
{
    /// <summary>
    /// The recorded parameters after initial_source_connection_id, without version_information and
    /// with max_datagram_frame_size 1024 (0x20, a two-byte varint 0x4400) in its place.
    /// </summary>
    internal const string ClientParametersOfferingDatagramFramesOf1024Bytes =
        "0504800080000604800080000704be8000000404be800000080480040000090480040000" + "20024400";

    [TestMethod]
    public void ReceiveDatagram_OptionsSetMaxDatagramFrameSize_SendsTheParameterLast()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(DatagramOptions(key, 1200));

        exchange.SendRecordedInitials();

        var sent = exchange.Client.Tls.ServerTransportParameters();
        Assert.AreEqual("200244B0", Convert.ToHexString(sent[^4..]), "Parameter 0x20, length 2, the varint 1200.");
        Assert.AreEqual(1200UL, QuicTransportParameters.Read(sent).MaxDatagramFrameSize);
    }

    [TestMethod]
    public void ReceiveDatagram_OptionsLeaveMaxDatagramFrameSizeUnset_SendNoSuchParameter()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));

        exchange.SendRecordedInitials();

        Assert.IsNull(QuicTransportParameters.Read(exchange.Client.Tls.ServerTransportParameters()).MaxDatagramFrameSize);
    }

    [TestMethod]
    public void ReceiveDatagram_DatagramFrameWithoutLength_DeliversItsDataAndAcknowledgesIt()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(DatagramOptions(key, 6));
        var acksBefore = OneRttFrames(exchange).OfType<QuicAckFrame>().Count();

        var output = exchange.Send(exchange.Client.SealOneRttPayload(Convert.FromHexString("3068656c6c6f")));

        var received = output.ReceivedDatagramFrames.Single();
        CollectionAssert.AreEqual("hello"u8.ToArray(), received.Data.ToArray());
        Assert.IsFalse(received.HasLength);
        Assert.IsFalse(exchange.Server.IsClosed);
        Assert.HasCount(acksBefore + 1, OneRttFrames(exchange).OfType<QuicAckFrame>().ToList(), "A DATAGRAM frame is ack-eliciting (RFC 9221 section 5).");
    }

    [TestMethod]
    public void ReceiveDatagram_DatagramFrameWithLength_DeliversItsDataAndReadsTheFrameAfterIt()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(DatagramOptions(key, 7));

        var output = exchange.Send(exchange.Client.SealOneRttPayload(Convert.FromHexString("310568656c6c6f" + "3102" + "6869")));

        Assert.HasCount(2, output.ReceivedDatagramFrames);
        CollectionAssert.AreEqual("hello"u8.ToArray(), output.ReceivedDatagramFrames[0].Data.ToArray());
        Assert.IsTrue(output.ReceivedDatagramFrames[0].HasLength);
        CollectionAssert.AreEqual("hi"u8.ToArray(), output.ReceivedDatagramFrames[1].Data.ToArray());
        Assert.IsFalse(exchange.Server.IsClosed);
    }

    [TestMethod]
    public void ReceiveDatagram_DatagramFrameWhenTheParameterWasNotSent_EndsWithProtocolViolation()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);

        var output = exchange.Send(exchange.Client.SealOneRttPayload(Convert.FromHexString("310568656c6c6f")));

        AssertClosedWithProtocolViolation(exchange, 0x31);
        Assert.IsEmpty(output.ReceivedDatagramFrames);
    }

    [TestMethod]
    public void ReceiveDatagram_DatagramFrameLargerThanTheParameterSent_EndsWithProtocolViolation()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(DatagramOptions(key, 6));

        var output = exchange.Send(exchange.Client.SealOneRttPayload(Convert.FromHexString("3068656c6c6f21")));

        AssertClosedWithProtocolViolation(exchange, 0x30);
        Assert.IsEmpty(output.ReceivedDatagramFrames);
    }

    [TestMethod]
    public void ReceiveDatagram_DatagramFrameWhenTheParameterSentIsZero_EndsWithProtocolViolation()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(DatagramOptions(key, 0));

        exchange.Send(exchange.Client.SealOneRttPayload(Convert.FromHexString("3100")));

        AssertClosedWithProtocolViolation(exchange, 0x31);
    }

    [TestMethod]
    public void SendDatagram_ClientOffersDatagramFrames_SendsTheDataInADatagramFrameWithLength()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(UnitTestCertificates.Options(key), UnitTestClientHelloEdits.ReplaceParameters(ClientParametersOfferingDatagramFramesOf1024Bytes));

        exchange.Take(exchange.Server.SendDatagram("hello"u8.ToArray()));

        Assert.AreEqual(1024UL, exchange.Server.PeerTransportParameters!.MaxDatagramFrameSize);
        var sent = OneRttFrames(exchange).OfType<QuicDatagramFrame>().Single();
        Assert.AreEqual("310568656C6C6F", Convert.ToHexString(sent.ToBytes()));
    }

    [TestMethod]
    public void SendDatagram_DatagramLost_IsNotSentAgainButCountsAsACongestionEvent()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(UnitTestCertificates.Options(key), UnitTestClientHelloEdits.ReplaceParameters(ClientParametersOfferingDatagramFramesOf1024Bytes));
        exchange.AcknowledgeOneRtt();
        var dropped = exchange.Server.SendDatagram("lost"u8.ToArray()).Datagrams.Single();
        var inFlightWithIt = exchange.Server.Congestion.BytesInFlight;
        foreach (var later in (string[])["one", "two", "three"])
        {
            exchange.Take(exchange.Server.SendDatagram(System.Text.Encoding.ASCII.GetBytes(later)));
        }

        var output = exchange.AcknowledgeOneRtt();

        Assert.IsGreaterThanOrEqualTo(dropped.Length, inFlightWithIt, "The DATAGRAM packet counted in flight.");
        Assert.IsEmpty(output.Datagrams, "Nothing is sent again.");
        Assert.AreEqual(0, exchange.Server.Congestion.BytesInFlight, "The lost packet left flight.");
        Assert.IsLessThan(QuicCongestionController.InitialWindow, exchange.Server.Congestion.CongestionWindow, "The loss halved the window (RFC 9002 section 7.3.2).");
        var data = OneRttFrames(exchange).OfType<QuicDatagramFrame>().Select(frame => System.Text.Encoding.ASCII.GetString(frame.Data.Span)).ToList();
        CollectionAssert.AreEqual(new[] { "one", "two", "three" }, data);
        exchange.Time.Advance(TimeSpan.FromSeconds(5));
        Assert.IsEmpty(exchange.Server.HandleTimeout().Datagrams, "No probe either: nothing ack-eliciting is in flight.");
    }

    [TestMethod]
    [DataRow("no max_datagram_frame_size", "0404be800000")]
    [DataRow("max_datagram_frame_size 0", "0404be800000" + "200100")]
    public void SendDatagram_ClientTakesNoDatagramFrames_Throws(string description, string clientParameters)
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(UnitTestCertificates.Options(key), UnitTestClientHelloEdits.ReplaceParameters(clientParameters));

        Assert.ThrowsExactly<InvalidOperationException>(() => exchange.Server.SendDatagram("hello"u8.ToArray()), description);
    }

    [TestMethod]
    public void SendDatagram_FrameLargerThanTheClientTakes_Throws()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(UnitTestCertificates.Options(key), UnitTestClientHelloEdits.ReplaceParameters(ClientParametersOfferingDatagramFramesOf1024Bytes));

        exchange.Take(exchange.Server.SendDatagram(new byte[1021]));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => exchange.Server.SendDatagram(new byte[1022]), "1 + 2 + 1022 bytes is 1025, over 1024.");
        Assert.HasCount(1, OneRttFrames(exchange).OfType<QuicDatagramFrame>().ToList());
    }

    [TestMethod]
    public void SendDatagram_BeforeTheHandshakeCompletes_Throws()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(DatagramOptions(key, 1200));

        exchange.SendRecordedInitials();

        Assert.ThrowsExactly<InvalidOperationException>(() => exchange.Server.SendDatagram("hello"u8.ToArray()));
    }

    private static QuicServerConnectionOptions DatagramOptions(RSA key, ulong maxDatagramFrameSize) => new()
    {
        CertificateChain = [UnitTestCertificates.SelfSigned(key)],
        CertificateKey = key,
        MaxDatagramFrameSize = maxDatagramFrameSize,
    };

    private static void AssertClosedWithProtocolViolation(UnitTestExchange exchange, ulong frameType)
    {
        var close = OneRttFrames(exchange).OfType<QuicConnectionCloseFrame>().Single();
        Assert.AreEqual((ulong)QuicTransportErrorCode.ProtocolViolation, close.ErrorCode);
        Assert.AreEqual(frameType, close.FrameType);
        Assert.IsTrue(exchange.Server.IsClosed);
    }
}
