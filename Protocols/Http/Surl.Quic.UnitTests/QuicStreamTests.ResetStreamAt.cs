using System.Security.Cryptography;

namespace Surl.Quic;

/// <summary>
/// <c>RESET_STREAM_AT</c> (BL-748, ADR-0117 decision 0). Every byte is written from
/// draft-ietf-quic-reliable-stream-reset-09: <c>reset_stream_at</c> is transport parameter 0x1d with
/// an empty value (section 3), and the frame is type 0x24 carrying Stream ID, Application Protocol
/// Error Code, Final Size and Reliable Size (section 4).
/// </summary>
public sealed partial class QuicStreamTests
{
    /// <summary>The recorded parameters after initial_source_connection_id, with reset_stream_at (0x1d, empty) added.</summary>
    internal const string ClientParametersOfferingResetStreamAt =
        "0504800080000604800080000704be8000000404be800000080480040000090480040000" + "1d00";

    [TestMethod]
    public void ReceiveDatagram_OptionsSetResetStreamAt_SendsTheEmptyParameterLast()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(ResetStreamAtOptions(key));

        exchange.SendRecordedInitials();

        var sent = exchange.Client.Tls.ServerTransportParameters();
        Assert.AreEqual("1D00", Convert.ToHexString(sent[^2..]), "Parameter 0x1d, length 0.");
        Assert.IsTrue(QuicTransportParameters.Read(sent).ResetStreamAt);
    }

    [TestMethod]
    public void ReceiveDatagram_OptionsLeaveResetStreamAtUnset_SendNoSuchParameterButReadTheClients()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(UnitTestCertificates.Options(key), UnitTestClientHelloEdits.ReplaceParameters(ClientParametersOfferingResetStreamAt));

        Assert.IsFalse(QuicTransportParameters.Read(exchange.Client.Tls.ServerTransportParameters()).ResetStreamAt);
        Assert.IsTrue(exchange.Server.PeerTransportParameters!.ResetStreamAt);
    }

    [TestMethod]
    public void ResetAt_BothSentTheParameter_SendsTheBytesBelowTheReliableSizeThenResetStreamAt()
    {
        using var key = RSA.Create(2048);
        using var exchange = ResetStreamAtExchange(key);
        var stream = SendFrames(exchange, Data(0, 0, "request")).Streams.Single();
        stream.Write("headerbody"u8, fin: true);

        stream.ResetAt(7, 6);
        exchange.Take(exchange.Server.Transmit());

        var data = Sent<QuicStreamFrame>(exchange).Single();
        Assert.AreEqual("header", ReadText(data));
        Assert.IsFalse(data.Fin, "The FIN written goes with the bytes past the Reliable Size.");
        Assert.AreEqual("2400070606", Convert.ToHexString(Sent<QuicResetStreamAtFrame>(exchange).Single().ToBytes()), "Stream 0, error 7, Final Size 6, Reliable Size 6.");
        Assert.IsEmpty(Sent<QuicResetStreamFrame>(exchange));
        Assert.ThrowsExactly<InvalidOperationException>(() => stream.Write("more"u8));
    }

    [TestMethod]
    public void ResetAt_ReliableSizeBelowWhatWasSent_SendsResetStreamAtWithTheSentFinalSize()
    {
        using var key = RSA.Create(2048);
        using var exchange = ResetStreamAtExchange(key);
        var stream = SendFrames(exchange, Data(0, 0, "request")).Streams.Single();
        stream.Write("abcdefgh"u8);
        exchange.Take(exchange.Server.Transmit());

        stream.ResetAt(7, 3);
        stream.ResetAt(8, 2);
        stream.Reset(9);
        exchange.Take(exchange.Server.Transmit());

        Assert.AreEqual("2400070803", Convert.ToHexString(Sent<QuicResetStreamAtFrame>(exchange).Single().ToBytes()), "Only the first reset is sent: Final Size 8, Reliable Size 3.");
        Assert.IsEmpty(Sent<QuicResetStreamFrame>(exchange));
    }

    [TestMethod]
    public void ResetAt_PacketsLost_SendsAgainOnlyTheBytesBelowTheReliableSizeAndTheReset()
    {
        using var key = RSA.Create(2048);
        using var exchange = ResetStreamAtExchange(key);
        var stream = SendFrames(exchange, Data(0, 0, "request")).Streams.Single();
        exchange.AcknowledgeOneRtt();
        foreach (var part in (string[])["abc", "defgh", "ij"])
        {
            stream.Write(System.Text.Encoding.ASCII.GetBytes(part));
            exchange.Server.Transmit();
        }

        stream.ResetAt(7, 4);
        exchange.Server.Transmit();
        for (var ping = 0; ping < 3; ping++)
        {
            exchange.Send(exchange.Client.SealOneRtt(new QuicPingFrame()));
        }

        exchange.AcknowledgeOneRtt();

        var resent = Sent<QuicStreamFrame>(exchange);
        CollectionAssert.AreEqual(
            new[] { "0A0003616263", "0E00030164" },
            resent.Select(frame => Convert.ToHexString(frame.ToBytes())).ToArray(),
            "Bytes 0 to 3 are sent again (\"abc\", then \"d\" cut from \"defgh\" without FIN); \"efgh\" and \"ij\" are not.");
        Assert.AreEqual("2400070A04", Convert.ToHexString(Sent<QuicResetStreamAtFrame>(exchange).Single().ToBytes()), "Final Size 10, Reliable Size 4.");
    }

    [TestMethod]
    public void ResetAt_ClientDidNotSendTheParameter_SendsResetStream()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(ResetStreamAtOptions(key));
        var stream = SendFrames(exchange, Data(0, 0, "request")).Streams.Single();
        stream.Write("header"u8);

        stream.ResetAt(7, 6);
        exchange.Take(exchange.Server.Transmit());

        Assert.AreEqual(new QuicResetStreamFrame(0, 7, 0), Sent<QuicResetStreamFrame>(exchange).Single());
        Assert.IsEmpty(Sent<QuicResetStreamAtFrame>(exchange));
        Assert.IsEmpty(Sent<QuicStreamFrame>(exchange));
    }

    [TestMethod]
    public void ResetAt_ConnectionDidNotAsk_SendsWhatResetSends()
    {
        using var key = RSA.Create(2048);
        using var asked = UnitTestExchange.Completed(UnitTestCertificates.Options(key), UnitTestClientHelloEdits.ReplaceParameters(ClientParametersOfferingResetStreamAt));
        using var plain = UnitTestExchange.Completed(UnitTestCertificates.Options(key), UnitTestClientHelloEdits.ReplaceParameters(ClientParametersOfferingResetStreamAt));
        var resetAt = SendFrames(asked, Data(0, 0, "request")).Streams.Single();
        var reset = SendFrames(plain, Data(0, 0, "request")).Streams.Single();
        resetAt.Write("header"u8);
        reset.Write("header"u8);

        resetAt.ResetAt(7, 6);
        reset.Reset(7);
        asked.Take(asked.Server.Transmit());
        plain.Take(plain.Server.Transmit());

        Assert.AreEqual(Sent<QuicResetStreamFrame>(plain).Single(), Sent<QuicResetStreamFrame>(asked).Single());
        Assert.IsEmpty(Sent<QuicResetStreamAtFrame>(asked));
        Assert.IsEmpty(Sent<QuicStreamFrame>(asked));
    }

    [TestMethod]
    public void ResetAt_FinAlreadySent_LeavesTheStreamAsItIs()
    {
        using var key = RSA.Create(2048);
        using var exchange = ResetStreamAtExchange(key);
        var stream = SendFrames(exchange, Data(0, 0, "request")).Streams.Single();
        stream.Write("done"u8, fin: true);
        exchange.Take(exchange.Server.Transmit());

        stream.ResetAt(7, 2);
        exchange.Take(exchange.Server.Transmit());

        Assert.IsEmpty(Sent<QuicResetStreamAtFrame>(exchange));
    }

    [TestMethod]
    public void ResetAt_ReliableSizePastWhatWasWrittenOrNoSendingPart_Throws()
    {
        using var key = RSA.Create(2048);
        using var exchange = ResetStreamAtExchange(key);
        var streams = SendFrames(exchange, Data(0, 0, "request"), Data(2, 0, "one way")).Streams;
        streams[0].Write("abc"u8);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => streams[0].ResetAt(7, 4));
        Assert.ThrowsExactly<InvalidOperationException>(() => streams[1].ResetAt(7, 0));
    }

    [TestMethod]
    public void Read_ResetStreamAtReceived_DeliversTheBytesBelowTheReliableSizeBeforeTheReset()
    {
        using var key = RSA.Create(2048);
        using var exchange = ResetStreamAtExchange(key);
        var stream = SendFrames(exchange, Data(0, 0, "hel")).Streams.Single();
        SendFrames(exchange, new QuicResetStreamAtFrame(0, 9, 11, 5));

        var first = ReadText(stream);
        var resetAfterFirst = stream.ResetErrorCode;
        SendFrames(exchange, Data(0, 3, "lo world"));
        var second = ReadText(stream);

        Assert.AreEqual("hel", first);
        Assert.IsNull(resetAfterFirst, "Bytes below the Reliable Size are still to come.");
        Assert.AreEqual("lo", second, "Bytes past the Reliable Size are never delivered.");
        Assert.AreEqual(9UL, stream.ResetErrorCode);
        Assert.AreEqual(0, stream.Read(new byte[4]));
        Assert.IsFalse(stream.IsReadComplete);
        Assert.IsFalse(exchange.Server.IsClosed);
    }

    [TestMethod]
    public void Read_LaterResetStreamAt_OnlyLowersTheReliableSize()
    {
        using var key = RSA.Create(2048);
        using var exchange = ResetStreamAtExchange(key);
        var stream = SendFrames(exchange, Data(0, 0, "abcdef")).Streams.Single();

        SendFrames(exchange, new QuicResetStreamAtFrame(0, 9, 6, 4));
        var afterFirst = stream.ReadableLength;
        SendFrames(exchange, new QuicResetStreamAtFrame(0, 9, 6, 6));
        var afterLarger = stream.ReadableLength;
        SendFrames(exchange, new QuicResetStreamAtFrame(0, 9, 6, 2));

        Assert.AreEqual(4, afterFirst);
        Assert.AreEqual(4, afterLarger, "A larger Reliable Size is ignored (section 5.1).");
        Assert.AreEqual("ab", ReadText(stream));
        Assert.AreEqual(9UL, stream.ResetErrorCode);
    }

    [TestMethod]
    public void ReceiveDatagram_ResetStreamAtBelowWhatWasRead_TakesEffectAtOnce()
    {
        using var key = RSA.Create(2048);
        using var exchange = ResetStreamAtExchange(key);
        var stream = SendFrames(exchange, Data(0, 0, "hello")).Streams.Single();
        ReadText(stream);

        SendFrames(exchange, new QuicResetStreamAtFrame(0, 9, 8, 2));

        Assert.AreEqual(9UL, stream.ResetErrorCode);
        Assert.IsFalse(exchange.Server.IsClosed);
    }

    [TestMethod]
    public void ReceiveDatagram_ResetStreamAtWithAnotherErrorCode_EndsWithStreamStateError()
    {
        using var key = RSA.Create(2048);
        using var exchange = ResetStreamAtExchange(key);
        SendFrames(exchange, Data(0, 0, "abcdef"), new QuicResetStreamAtFrame(0, 9, 6, 4));

        SendFrames(exchange, new QuicResetStreamAtFrame(0, 10, 6, 2));

        AssertClosedWith(exchange, QuicTransportErrorCode.StreamStateError, 0x24);
    }

    [TestMethod]
    public void ReceiveDatagram_ResetStreamAtReliableSizeAboveFinalSize_EndsWithFrameEncodingError()
    {
        using var key = RSA.Create(2048);
        using var exchange = ResetStreamAtExchange(key);

        exchange.Send(exchange.Client.SealOneRttPayload(Convert.FromHexString("2400070511")));

        AssertClosedWith(exchange, QuicTransportErrorCode.FrameEncodingError, 0x24);
    }

    [TestMethod]
    public void ReceiveDatagram_ResetStreamAtWhenTheServerDidNotAsk_EndsWithFrameEncodingError()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(UnitTestCertificates.Options(key), UnitTestClientHelloEdits.ReplaceParameters(ClientParametersOfferingResetStreamAt));

        exchange.Send(exchange.Client.SealOneRttPayload(Convert.FromHexString("2400070505")));

        AssertClosedWith(exchange, QuicTransportErrorCode.FrameEncodingError, 0x24);
    }

    private static QuicServerConnectionOptions ResetStreamAtOptions(RSA key) => new()
    {
        CertificateChain = [UnitTestCertificates.SelfSigned(key)],
        CertificateKey = key,
        ResetStreamAt = true,
    };

    private static UnitTestExchange ResetStreamAtExchange(RSA key) =>
        UnitTestExchange.Completed(ResetStreamAtOptions(key), UnitTestClientHelloEdits.ReplaceParameters(ClientParametersOfferingResetStreamAt));

    private static string ReadText(QuicStreamFrame frame) => System.Text.Encoding.ASCII.GetString(frame.Data.Span);
}
