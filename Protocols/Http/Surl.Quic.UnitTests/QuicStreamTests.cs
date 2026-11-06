using System.Security.Cryptography;
using System.Text;

namespace Surl.Quic;

/// <summary>
/// Streams on a <see cref="QuicServerConnection"/> whose handshake with the test client is
/// complete: the test client seals the frames RFC 9000 sections 2 to 4 and 19.4 to 19.14 describe,
/// and every expected value is what those sections say. Packets are lost only where a test drops
/// them (BL-379): QuicStreamTests.Loss.cs and QuicStreamTests.LossyExchange.cs.
/// </summary>
[TestClass]
public sealed partial class QuicStreamTests
{
    [TestMethod]
    public void Read_StreamFramesOutOfOrder_ReturnsTheBytesInOrder()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);

        var first = SendFrames(exchange, Data(0, 5, " world", fin: true));
        var stream = first.Streams.Single();
        var readableBeforeTheGapFills = stream.ReadableLength;
        SendFrames(exchange, Data(0, 0, "hello"));

        Assert.AreEqual(0, readableBeforeTheGapFills);
        Assert.AreEqual("hello world", ReadText(stream));
        Assert.IsTrue(stream.IsReadComplete);
        Assert.AreEqual((ulong)0, stream.Id);
        Assert.IsTrue(stream.IsClientInitiated);
        Assert.IsTrue(stream.IsBidirectional);
        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void Read_DuplicateAndOverlappingStreamData_ReturnsEachByteOnce()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);

        var output = SendFrames(exchange, Data(0, 0, "abc"), Data(0, 0, "abc"), Data(0, 1, "bcde"));

        var stream = output.Streams.Single();
        Assert.AreEqual("abcde", ReadText(stream));
        Assert.AreEqual(0, stream.Read(new byte[4]));
        Assert.IsFalse(stream.IsReadComplete);
        Assert.IsFalse(exchange.Server.IsClosed);
    }

    [TestMethod]
    public void ReceiveDatagram_ClientStream_OpensEveryLowerStreamOfItsKind()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        SendFrames(exchange, Data(8, 0, "c"));

        var output = SendFrames(exchange, Data(4, 0, "b"), Data(396, 0, "last"));

        CollectionAssert.AreEqual(new ulong[] { 4, 396 }, output.Streams.Select(stream => stream.Id).ToArray());
        Assert.IsFalse(exchange.Server.IsClosed);
    }

    [TestMethod]
    [DataRow(0UL, 262144UL)]
    [DataRow(2UL, 65536UL)]
    public void ReceiveDatagram_StreamDataBeyondTheStreamCredit_EndsWithFlowControlError(ulong streamId, ulong credit)
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        SendFrames(exchange, Data(streamId, credit - 1, "x"));

        SendFrames(exchange, Data(streamId, credit, "x"));

        AssertClosedWith(exchange, QuicTransportErrorCode.FlowControlError, 0x0e);
    }

    [TestMethod]
    public void ReceiveDatagram_StreamDataBeyondTheConnectionCredit_EndsWithFlowControlError()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(SmallCredit(key));
        SendFrames(exchange, Data(0, 0, new string('a', 100)));

        SendFrames(exchange, Data(4, 0, new string('b', 100)), Data(8, 0, "c"));

        AssertClosedWith(exchange, QuicTransportErrorCode.FlowControlError, 0x0a);
    }

    [TestMethod]
    public void ReceiveDatagram_ResetStreamBeyondTheStreamCredit_EndsWithFlowControlError()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(SmallCredit(key));

        SendFrames(exchange, new QuicResetStreamFrame(0, 0x10c, 101));

        AssertClosedWith(exchange, QuicTransportErrorCode.FlowControlError, 0x04);
    }

    [TestMethod]
    [DataRow(400UL)]
    [DataRow(14UL)]
    public void ReceiveDatagram_ClientStreamBeyondMaxStreams_EndsWithStreamLimitError(ulong streamId)
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);

        SendFrames(exchange, Data(streamId, 0, "x"));

        AssertClosedWith(exchange, QuicTransportErrorCode.StreamLimitError, 0x0a);
    }

    [TestMethod]
    [DataRow("0a030161", 0x0a, DisplayName = "STREAM on the server's unidirectional stream 3")]
    [DataRow("0a010161", 0x0a, DisplayName = "STREAM on server bidirectional stream 1, never opened")]
    [DataRow("04030000", 0x04, DisplayName = "RESET_STREAM on the server's unidirectional stream 3")]
    [DataRow("150300", 0x15, DisplayName = "STREAM_DATA_BLOCKED on the server's unidirectional stream 3")]
    [DataRow("050200", 0x05, DisplayName = "STOP_SENDING on the client's unidirectional stream 2")]
    [DataRow("110200", 0x11, DisplayName = "MAX_STREAM_DATA on the client's unidirectional stream 2")]
    [DataRow("050100", 0x05, DisplayName = "STOP_SENDING on server bidirectional stream 1, never opened")]
    [DataRow("050700", 0x05, DisplayName = "STOP_SENDING on server unidirectional stream 7, not yet opened")]
    public void ReceiveDatagram_FrameForAStreamPartThatIsNotOpen_EndsWithStreamStateError(string frameHex, int frameType)
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        exchange.Server.OpenUnidirectionalStream();

        SendFrames(exchange, QuicFrameReader.Read(Convert.FromHexString(frameHex), QuicPacketType.OneRtt).Single());

        AssertClosedWith(exchange, QuicTransportErrorCode.StreamStateError, (ulong)frameType);
    }

    [TestMethod]
    public void ReceiveDatagram_DataPastTheFinalSize_EndsWithFinalSizeError()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        SendFrames(exchange, Data(0, 0, "abc", fin: true));

        SendFrames(exchange, Data(0, 3, "d"));

        AssertClosedWith(exchange, QuicTransportErrorCode.FinalSizeError, 0x0e);
    }

    [TestMethod]
    public void ReceiveDatagram_SecondFinAtAnotherSize_EndsWithFinalSizeError()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        SendFrames(exchange, Data(0, 0, "abc", fin: true));

        SendFrames(exchange, Data(0, 0, "ab", fin: true));

        AssertClosedWith(exchange, QuicTransportErrorCode.FinalSizeError, 0x0b);
    }

    [TestMethod]
    public void ReceiveDatagram_FinBelowTheDataReceived_EndsWithFinalSizeError()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        SendFrames(exchange, Data(0, 0, "abcd"));

        SendFrames(exchange, Data(0, 0, "ab", fin: true));

        AssertClosedWith(exchange, QuicTransportErrorCode.FinalSizeError, 0x0b);
    }

    [TestMethod]
    public void ReceiveDatagram_ResetStreamAtAnotherFinalSize_EndsWithFinalSizeError()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        SendFrames(exchange, Data(0, 0, "abc", fin: true));

        SendFrames(exchange, new QuicResetStreamFrame(0, 0x10c, 5));

        AssertClosedWith(exchange, QuicTransportErrorCode.FinalSizeError, 0x04);
    }

    [TestMethod]
    public void ReceiveDatagram_DataWithinAKnownFinalSize_IsAccepted()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);

        var output = SendFrames(exchange, Data(0, 0, "abc", fin: true), Data(0, 1, "bc", fin: true), Data(0, 0, "ab"));

        Assert.AreEqual("abc", ReadText(output.Streams.Single()));
        Assert.IsFalse(exchange.Server.IsClosed);
    }

    [TestMethod]
    public void ReceiveDatagram_ResetStream_DiscardsUnreadDataReportsTheCodeAndFreesConnectionCredit()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(SmallCredit(key));
        var stream = SendFrames(exchange, Data(0, 0, "abc")).Streams.Single();

        SendFrames(exchange, new QuicResetStreamFrame(0, 0x10c, 50), new QuicResetStreamFrame(0, 0x10d, 50), Data(0, 0, "abc"));
        exchange.Take(exchange.Server.Transmit());
        var maxDataBeforeTheSecondReset = Sent<QuicMaxDataFrame>(exchange).Count;
        SendFrames(exchange, new QuicResetStreamFrame(4, 0x10c, 60));
        exchange.Take(exchange.Server.Transmit());

        Assert.AreEqual((ulong)0x10c, stream.ResetErrorCode);
        Assert.AreEqual(0, stream.ReadableLength);
        Assert.IsFalse(stream.IsReadComplete);
        Assert.AreEqual(0, maxDataBeforeTheSecondReset);
        Assert.AreEqual(new QuicMaxDataFrame(310), Sent<QuicMaxDataFrame>(exchange).Single());
        Assert.IsFalse(exchange.Server.IsClosed);
    }

    [TestMethod]
    public void Read_PastHalfTheWindow_GrantsMaxStreamDataAndMaxData()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(SmallCredit(key));
        var stream = SendFrames(exchange, Data(0, 0, new string('a', 60))).Streams.Single();

        ReadText(stream);
        exchange.Take(exchange.Server.Transmit());
        SendFrames(exchange, Data(0, 60, new string('b', 100)));
        ReadText(stream);
        exchange.Take(exchange.Server.Transmit());
        SendFrames(exchange, Data(0, 160, new string('c', 100)));

        CollectionAssert.AreEqual(
            new[] { new QuicMaxStreamDataFrame(0, 160), new QuicMaxStreamDataFrame(0, 260) },
            Sent<QuicMaxStreamDataFrame>(exchange));
        Assert.AreEqual(new QuicMaxDataFrame(360), Sent<QuicMaxDataFrame>(exchange).Single());
        Assert.AreEqual(100, stream.ReadableLength);
        Assert.IsFalse(exchange.Server.IsClosed);
    }

    [TestMethod]
    public void Transmit_ClientStreamsBothOfWhosePartsAreDone_GrantsMaxStreams()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        var output = SendFrames(exchange, Data(0, 0, "request", fin: true), Data(2, 0, "control", fin: true));
        var bidirectional = output.Streams[0];

        ReadText(bidirectional);
        ReadText(output.Streams[1]);
        bidirectional.Write(Encoding.ASCII.GetBytes("response"), fin: true);
        exchange.Take(exchange.Server.Transmit());
        var afterClose = SendFrames(exchange, Data(0, 0, "request", fin: true), new QuicStopSendingFrame(0, 1));

        CollectionAssert.AreEqual(
            new[] { new QuicMaxStreamsFrame(true, 101), new QuicMaxStreamsFrame(false, 4) },
            Sent<QuicMaxStreamsFrame>(exchange));
        Assert.AreEqual(new QuicStreamFrame(0, 0, Encoding.ASCII.GetBytes("response"), true), Sent<QuicStreamFrame>(exchange).Single(), StreamFrameComparer);
        Assert.IsEmpty(afterClose.Streams);
        Assert.IsFalse(exchange.Server.IsClosed);
    }

    [TestMethod]
    public void ReceiveDatagram_StopSending_IsAnsweredWithResetStreamCarryingItsCodeOnce()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        var stream = SendFrames(exchange, Data(0, 0, "request")).Streams.Single();
        stream.Write(Encoding.ASCII.GetBytes("abc"));
        exchange.Take(exchange.Server.Transmit());

        SendFrames(exchange, new QuicStopSendingFrame(0, 0x10c), new QuicStopSendingFrame(0, 0x10d));

        Assert.AreEqual(new QuicResetStreamFrame(0, 0x10c, 3), Sent<QuicResetStreamFrame>(exchange).Single());
        Assert.AreEqual((ulong)0x10c, stream.StopSendingErrorCode);
        Assert.ThrowsExactly<InvalidOperationException>(() => stream.Write([1]));
    }

    [TestMethod]
    public void Reset_ServerSendingPart_SendsResetStreamOnceWithTheFinalSize()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        var stream = SendFrames(exchange, Data(0, 0, "request")).Streams.Single();
        stream.Write(Encoding.ASCII.GetBytes("ab"));
        exchange.Take(exchange.Server.Transmit());

        stream.Reset(0x10c);
        stream.Reset(0x10d);
        stream.StopSending(0x10e);
        exchange.Take(exchange.Server.Transmit());

        Assert.AreEqual(new QuicResetStreamFrame(0, 0x10c, 2), Sent<QuicResetStreamFrame>(exchange).Single());
        Assert.AreEqual(new QuicStopSendingFrame(0, 0x10e), Sent<QuicStopSendingFrame>(exchange).Single());
        Assert.ThrowsExactly<InvalidOperationException>(() => stream.Write([1]));
    }

    [TestMethod]
    public void Reset_AfterTheFinIsSent_SendsNothing()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        var stream = SendFrames(exchange, Data(0, 0, "request")).Streams.Single();
        stream.Write(Encoding.ASCII.GetBytes("ab"), fin: true);
        exchange.Take(exchange.Server.Transmit());

        stream.Reset(0x10c);
        exchange.Take(exchange.Server.Transmit());

        Assert.IsEmpty(Sent<QuicResetStreamFrame>(exchange));
        Assert.ThrowsExactly<InvalidOperationException>(() => stream.Write([1]));
    }

    [TestMethod]
    public void OpenUnidirectionalStream_WrittenWithFin_SendsTheDataOnStreamThreeThenForgetsTheStream()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);

        var stream = exchange.Server.OpenUnidirectionalStream()!;
        stream.Write(Encoding.ASCII.GetBytes("set"));
        stream.Write(Encoding.ASCII.GetBytes("tings"), fin: true);
        exchange.Take(exchange.Server.Transmit());
        var afterClose = SendFrames(exchange, new QuicStopSendingFrame(3, 0), new QuicMaxStreamDataFrame(3, 9));
        var next = exchange.Server.OpenUnidirectionalStream()!;

        Assert.AreEqual((ulong)3, stream.Id);
        Assert.IsFalse(stream.IsClientInitiated);
        Assert.IsFalse(stream.IsBidirectional);
        Assert.IsFalse(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
        Assert.AreEqual(new QuicStreamFrame(3, 0, Encoding.ASCII.GetBytes("settings"), true), Sent<QuicStreamFrame>(exchange).Single(), StreamFrameComparer);
        Assert.IsEmpty(afterClose.Streams);
        Assert.AreEqual((ulong)7, next.Id);
        Assert.IsEmpty(Sent<QuicMaxStreamsFrame>(exchange));
    }

    [TestMethod]
    public void Transmit_DataBeyondTheClientsStreamCredit_SendsStreamDataBlockedOnceUntilMaxStreamData()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(UnitTestCertificates.Options(key), UnitTestClientHelloEdits.Replace("0704be800000", "070480000400"));
        var stream = exchange.Server.OpenUnidirectionalStream()!;
        stream.Write(new byte[1500]);

        exchange.Take(exchange.Server.Transmit());
        exchange.Take(exchange.Server.Transmit());
        var unsentWhileBlocked = stream.UnsentLength;
        var output = SendFrames(exchange, new QuicMaxStreamDataFrame(3, 2000), new QuicMaxStreamDataFrame(3, 10));

        Assert.AreEqual(476, unsentWhileBlocked);
        Assert.AreEqual(new QuicStreamDataBlockedFrame(3, 1024), Sent<QuicStreamDataBlockedFrame>(exchange).Single());
        Assert.AreSame(stream, output.Streams.Single());
        Assert.AreEqual(0, stream.UnsentLength);
        Assert.AreEqual(1500, Sent<QuicStreamFrame>(exchange).Sum(frame => frame.Data.Length));
        Assert.AreEqual((ulong)1024, Sent<QuicStreamFrame>(exchange).Last().Offset);
    }

    [TestMethod]
    public void Reset_WhileDataAndFinAreHeldBackByCredit_DiscardsThemAndSendsResetStreamAtTheOffsetSent()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(UnitTestCertificates.Options(key), UnitTestClientHelloEdits.Replace("0704be800000", "070480000400"));
        var stream = exchange.Server.OpenUnidirectionalStream()!;
        stream.Write(new byte[1500], fin: true);
        exchange.Take(exchange.Server.Transmit());

        stream.Reset(0x10c);
        exchange.Take(exchange.Server.Transmit());
        SendFrames(exchange, new QuicMaxStreamDataFrame(3, 2000));

        Assert.AreEqual(new QuicResetStreamFrame(3, 0x10c, 1024), Sent<QuicResetStreamFrame>(exchange).Single());
        Assert.AreEqual(0, stream.UnsentLength);
        Assert.AreEqual(1024, Sent<QuicStreamFrame>(exchange).Sum(frame => frame.Data.Length));
        Assert.IsFalse(Sent<QuicStreamFrame>(exchange).Any(frame => frame.Fin));
    }

    [TestMethod]
    public void Transmit_DataBeyondTheClientsConnectionCredit_SendsDataBlockedOnceUntilMaxData()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(UnitTestCertificates.Options(key), UnitTestClientHelloEdits.Replace("0404be800000", "040480000400"));
        var stream = exchange.Server.OpenUnidirectionalStream()!;
        stream.Write(new byte[1024]);
        exchange.Take(exchange.Server.Transmit());
        var blockedWithNothingWaiting = Sent<QuicDataBlockedFrame>(exchange).Count;

        stream.Write(new byte[476]);
        exchange.Take(exchange.Server.Transmit());
        exchange.Take(exchange.Server.Transmit());
        var unsentWhileBlocked = stream.UnsentLength;
        SendFrames(exchange, new QuicMaxDataFrame(3000), new QuicMaxDataFrame(10));

        Assert.AreEqual(0, blockedWithNothingWaiting);
        Assert.AreEqual(476, unsentWhileBlocked);
        Assert.AreEqual(new QuicDataBlockedFrame(1024), Sent<QuicDataBlockedFrame>(exchange).Single());
        Assert.IsEmpty(Sent<QuicStreamDataBlockedFrame>(exchange));
        Assert.AreEqual(1500, Sent<QuicStreamFrame>(exchange).Sum(frame => frame.Data.Length));
    }

    [TestMethod]
    public void OpenUnidirectionalStream_BeyondTheClientsMaxStreams_ReturnsNullAndSendsStreamsBlockedOnce()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(UnitTestCertificates.Options(key), UnitTestClientHelloEdits.Replace("090480040000", "090480000001"));
        exchange.Server.OpenUnidirectionalStream();

        var refused = exchange.Server.OpenUnidirectionalStream();
        var refusedAgain = exchange.Server.OpenUnidirectionalStream();
        exchange.Take(exchange.Server.Transmit());
        SendFrames(exchange, new QuicMaxStreamsFrame(true, 10));
        var refusedAfterBidirectionalCredit = exchange.Server.OpenUnidirectionalStream();
        SendFrames(exchange, new QuicMaxStreamsFrame(false, 2));
        var opened = exchange.Server.OpenUnidirectionalStream();

        Assert.IsNull(refused);
        Assert.IsNull(refusedAgain);
        Assert.IsNull(refusedAfterBidirectionalCredit);
        Assert.AreEqual(new QuicStreamsBlockedFrame(false, 1), Sent<QuicStreamsBlockedFrame>(exchange).Single());
        Assert.AreEqual((ulong)7, opened!.Id);
    }

    [TestMethod]
    public void ReceiveDatagram_BlockedFramesFromTheClient_NeedNoAnswer()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);

        var output = SendFrames(exchange, new QuicStreamDataBlockedFrame(0, 100), new QuicDataBlockedFrame(100), new QuicStreamsBlockedFrame(true, 100));

        Assert.AreEqual((ulong)0, output.Streams.Single().Id);
        Assert.IsFalse(exchange.Server.IsClosed);
    }

    [TestMethod]
    public void StreamMethods_OnAPartTheStreamDoesNotHave_Throw()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        var clientUnidirectional = SendFrames(exchange, Data(2, 0, "x")).Streams.Single();
        var serverUnidirectional = exchange.Server.OpenUnidirectionalStream()!;
        serverUnidirectional.Write([1], fin: true);

        Assert.IsFalse(clientUnidirectional.CanWrite);
        Assert.ThrowsExactly<InvalidOperationException>(() => clientUnidirectional.Write([1]));
        Assert.ThrowsExactly<InvalidOperationException>(() => clientUnidirectional.Reset(0));
        Assert.ThrowsExactly<InvalidOperationException>(() => serverUnidirectional.StopSending(0));
        Assert.ThrowsExactly<InvalidOperationException>(() => serverUnidirectional.Write([2]));
    }

    [TestMethod]
    public void OpenUnidirectionalStreamAndTransmit_BeforeTheHandshakeCompletesOrAfterTheConnectionEnds_Throw()
    {
        using var key = RSA.Create(2048);
        using var starting = new UnitTestExchange(UnitTestCertificates.Options(key));
        using var ended = UnitTestExchange.Completed(key);
        ended.Server.Close(0x100);

        Assert.ThrowsExactly<InvalidOperationException>(() => starting.Server.OpenUnidirectionalStream());
        Assert.ThrowsExactly<InvalidOperationException>(() => starting.Server.Transmit());
        Assert.ThrowsExactly<InvalidOperationException>(() => ended.Server.OpenUnidirectionalStream());
        Assert.ThrowsExactly<InvalidOperationException>(() => ended.Server.Transmit());
    }

    private static readonly StreamFrameEquality StreamFrameComparer = new();

    private static QuicServerConnectionOptions SmallCredit(RSA key)
    {
        var options = UnitTestCertificates.Options(key);
        return new QuicServerConnectionOptions
        {
            CertificateChain = options.CertificateChain,
            CertificateKey = key,
            InitialMaxData = 200,
            InitialMaxStreamDataBidiRemote = 100,
            InitialMaxStreamDataUni = 100,
        };
    }

    private static QuicStreamFrame Data(ulong streamId, ulong offset, string text, bool fin = false) =>
        new(streamId, offset, Encoding.ASCII.GetBytes(text), fin);

    private static QuicConnectionOutput SendFrames(UnitTestExchange exchange, params QuicFrame[] frames) =>
        exchange.Send(exchange.Client.SealOneRtt(frames));

    private static string ReadText(QuicStream stream)
    {
        var buffer = new byte[stream.ReadableLength];
        Assert.AreEqual(buffer.Length, stream.Read(buffer));
        return Encoding.ASCII.GetString(buffer);
    }

    private static List<T> Sent<T>(UnitTestExchange exchange) => [.. exchange.Client.FramesReceived[QuicPacketType.OneRtt].OfType<T>()];

    private static void AssertClosedWith(UnitTestExchange exchange, QuicTransportErrorCode errorCode, ulong frameType)
    {
        var close = Sent<QuicConnectionCloseFrame>(exchange).Single();
        Assert.AreEqual((ulong)errorCode, close.ErrorCode);
        Assert.AreEqual(frameType, close.FrameType);
        Assert.IsTrue(exchange.Server.IsClosed);
    }

    /// <summary>Compares STREAM frames by their bytes, since a record compares its <see cref="ReadOnlyMemory{T}"/> by reference.</summary>
    private sealed class StreamFrameEquality : IEqualityComparer<QuicStreamFrame>
    {
        public bool Equals(QuicStreamFrame? x, QuicStreamFrame? y) => x!.ToBytes().AsSpan().SequenceEqual(y!.ToBytes());

        public int GetHashCode(QuicStreamFrame obj) => obj.StreamId.GetHashCode();
    }
}
