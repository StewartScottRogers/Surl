using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ws;

[TestClass]
public sealed class WebSocketFrameReaderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadFrameAsync_UnmaskedTextHello_ReadsAFinishedTextFrame()
    {
        var frame = await ReadOneFrameAsync(Rfc6455Examples.UnmaskedTextHello);

        Assert.IsTrue(frame.Fin);
        Assert.AreEqual(WebSocketOpcode.Text, frame.Opcode);
        Assert.IsFalse(frame.Masked);
        CollectionAssert.AreEqual(Rfc6455Examples.Hello, frame.Payload);
    }

    [TestMethod]
    public async Task ReadFrameAsync_MaskedTextHello_UnmasksThePayload()
    {
        var frame = await ReadOneFrameAsync(Rfc6455Examples.MaskedTextHello);

        Assert.AreEqual(WebSocketOpcode.Text, frame.Opcode);
        Assert.IsTrue(frame.Masked);
        CollectionAssert.AreEqual(Rfc6455Examples.Hello, frame.Payload);
    }

    [TestMethod]
    public async Task ReadFrameAsync_MaskedTextHelloOneByteAtATime_UnmasksThePayload()
    {
        var chunks = Rfc6455Examples.MaskedTextHello.Select(b => (ReadOnlyMemory<byte>)new[] { b });
        var reader = new WebSocketFrameReader(new InMemoryConnection(chunks), 0);

        var result = await reader.ReadFrameAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(Rfc6455Examples.Hello, result.Frame!.Payload);
    }

    [TestMethod]
    public async Task ReadFrameAsync_FragmentedUnmaskedText_ReadsBothFramesInOrder()
    {
        var reader = ReaderOver(Rfc6455Examples.Join(Rfc6455Examples.FragmentedTextFirst, Rfc6455Examples.FragmentedTextSecond));

        var first = (await reader.ReadFrameAsync(TestContext.CancellationToken)).Frame!;
        var second = (await reader.ReadFrameAsync(TestContext.CancellationToken)).Frame!;

        Assert.IsFalse(first.Fin);
        Assert.AreEqual(WebSocketOpcode.Text, first.Opcode);
        CollectionAssert.AreEqual("Hel"u8.ToArray(), first.Payload);
        Assert.IsTrue(second.Fin);
        Assert.AreEqual(WebSocketOpcode.Continuation, second.Opcode);
        CollectionAssert.AreEqual("lo"u8.ToArray(), second.Payload);
    }

    [TestMethod]
    public async Task ReadFrameAsync_UnmaskedPing_ReadsAPingWithItsBody()
    {
        var frame = await ReadOneFrameAsync(Rfc6455Examples.UnmaskedPingHello);

        Assert.AreEqual(WebSocketOpcode.Ping, frame.Opcode);
        Assert.IsTrue(frame.IsControl);
        CollectionAssert.AreEqual(Rfc6455Examples.Hello, frame.Payload);
    }

    [TestMethod]
    public async Task ReadFrameAsync_MaskedPong_ReadsAPongWithItsBodyUnmasked()
    {
        var frame = await ReadOneFrameAsync(Rfc6455Examples.MaskedPongHello);

        Assert.AreEqual(WebSocketOpcode.Pong, frame.Opcode);
        Assert.IsTrue(frame.Masked);
        CollectionAssert.AreEqual(Rfc6455Examples.Hello, frame.Payload);
    }

    [TestMethod]
    public async Task ReadFrameAsync_256ByteBinary_ReadsTheSixteenBitLength()
    {
        var payload = Rfc6455Examples.CountingPayload(256);

        var frame = await ReadOneFrameAsync(Rfc6455Examples.Join(Rfc6455Examples.Binary256Header, payload));

        Assert.AreEqual(WebSocketOpcode.Binary, frame.Opcode);
        CollectionAssert.AreEqual(payload, frame.Payload);
    }

    [TestMethod]
    public async Task ReadFrameAsync_64KiBBinary_ReadsTheSixtyFourBitLength()
    {
        var payload = Rfc6455Examples.CountingPayload(65536);

        var frame = await ReadOneFrameAsync(Rfc6455Examples.Join(Rfc6455Examples.Binary64KiBHeader, payload));

        Assert.AreEqual(WebSocketOpcode.Binary, frame.Opcode);
        CollectionAssert.AreEqual(payload, frame.Payload);
    }

    [TestMethod]
    public async Task ReadFrameAsync_PayloadOver64KiB_GrowsItsBufferAsBytesArrive()
    {
        var payload = Rfc6455Examples.CountingPayload(70000);
        byte[] header = [0x82, 0x7F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x11, 0x70];

        var frame = await ReadOneFrameAsync(Rfc6455Examples.Join(header, payload));

        CollectionAssert.AreEqual(payload, frame.Payload);
    }

    [TestMethod]
    public async Task ReadFrameAsync_AllReservedBitsSet_ReportsEachOne()
    {
        var frame = await ReadOneFrameAsync([0xF2, 0x00]);

        Assert.IsTrue(frame.Rsv1);
        Assert.IsTrue(frame.Rsv2);
        Assert.IsTrue(frame.Rsv3);
        Assert.AreEqual(WebSocketOpcode.Binary, frame.Opcode);
        Assert.IsEmpty(frame.Payload);
    }

    [TestMethod]
    public async Task ReadFrameAsync_NoReservedBitSet_ReportsNone()
    {
        var frame = await ReadOneFrameAsync(Rfc6455Examples.UnmaskedTextHello);

        Assert.IsFalse(frame.Rsv1 || frame.Rsv2 || frame.Rsv3);
    }

    [TestMethod]
    public async Task ReadFrameAsync_NothingSent_IsConnectionClosed()
    {
        await AssertOutcomeAsync([], WebSocketFrameReadOutcome.ConnectionClosed);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x81 }, DisplayName = "inside the first two bytes")]
    [DataRow(new byte[] { 0x82, 0x7E, 0x01 }, DisplayName = "inside the 16-bit length")]
    [DataRow(new byte[] { 0x82, 0x7F, 0x00, 0x00, 0x00 }, DisplayName = "inside the 64-bit length")]
    [DataRow(new byte[] { 0x81, 0x85, 0x37, 0xfa }, DisplayName = "inside the masking key")]
    [DataRow(new byte[] { 0x81, 0x05, 0x48, 0x65 }, DisplayName = "inside the payload")]
    public async Task ReadFrameAsync_StreamEndsPartwayThroughAFrame_IsConnectionClosedMidFrame(byte[] bytes)
    {
        await AssertOutcomeAsync(bytes, WebSocketFrameReadOutcome.ConnectionClosedMidFrame);
    }

    [TestMethod]
    [DataRow(0x3)]
    [DataRow(0x7)]
    [DataRow(0xB)]
    [DataRow(0xF)]
    public async Task ReadFrameAsync_ReservedOpcode_IsReservedOpcode(int opcode)
    {
        await AssertOutcomeAsync([(byte)(0x80 | opcode), 0x00], WebSocketFrameReadOutcome.ReservedOpcode);
    }

    [TestMethod]
    [DataRow(0x8)]
    [DataRow(0x9)]
    [DataRow(0xA)]
    public async Task ReadFrameAsync_ControlFrameWithFinClear_IsFragmentedControlFrame(int opcode)
    {
        await AssertOutcomeAsync([(byte)opcode, 0x00], WebSocketFrameReadOutcome.FragmentedControlFrame);
    }

    [TestMethod]
    public async Task ReadFrameAsync_ControlFrameWithASixteenBitLength_IsControlFramePayloadTooLong()
    {
        await AssertOutcomeAsync([0x89, 0x7E, 0x00, 0x7E], WebSocketFrameReadOutcome.ControlFramePayloadTooLong);
    }

    [TestMethod]
    public async Task ReadFrameAsync_ControlFrameOf125Bytes_IsRead()
    {
        var frame = await ReadOneFrameAsync(Rfc6455Examples.Join([0x8A, 0x7D], new byte[125]));

        Assert.HasCount(125, frame.Payload);
    }

    [TestMethod]
    public async Task ReadFrameAsync_SixteenBitLengthUnder126_IsPayloadLengthNotMinimal()
    {
        await AssertOutcomeAsync([0x82, 0x7E, 0x00, 0x7D], WebSocketFrameReadOutcome.PayloadLengthNotMinimal);
    }

    [TestMethod]
    public async Task ReadFrameAsync_SixtyFourBitLengthUnder65536_IsPayloadLengthNotMinimal()
    {
        await AssertOutcomeAsync(
            [0x82, 0x7F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF],
            WebSocketFrameReadOutcome.PayloadLengthNotMinimal);
    }

    [TestMethod]
    public async Task ReadFrameAsync_SixtyFourBitLengthWithItsMostSignificantBitSet_IsReported()
    {
        await AssertOutcomeAsync(
            [0x82, 0x7F, 0x80, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00],
            WebSocketFrameReadOutcome.PayloadLengthMostSignificantBitSet);
    }

    [TestMethod]
    public async Task ReadFrameAsync_FrameOverTheMaximum_IsRefusedBeforeAnyPayloadByteIsRead()
    {
        byte[] header = [0x82, 0x7E, 0x00, 0xC8];
        var connection = new ReadCountingConnection(new InMemoryConnection([Rfc6455Examples.Join(header, new byte[200])]));
        var reader = new WebSocketFrameReader(connection, 203);

        var result = await reader.ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(WebSocketFrameReadOutcome.FrameTooLarge, result.Outcome);
        Assert.IsNull(result.Frame);
        Assert.AreEqual(header.Length, connection.BytesRead);
    }

    [TestMethod]
    public async Task ReadFrameAsync_MaskedFrameOverTheMaximumByItsMaskingKey_IsRefusedBeforeTheKeyIsRead()
    {
        var connection = new ReadCountingConnection(new InMemoryConnection([Rfc6455Examples.MaskedTextHello]));
        var reader = new WebSocketFrameReader(connection, Rfc6455Examples.MaskedTextHello.Length - 1);

        var result = await reader.ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(WebSocketFrameReadOutcome.FrameTooLarge, result.Outcome);
        Assert.AreEqual(2, connection.BytesRead);
    }

    [TestMethod]
    public async Task ReadFrameAsync_FrameExactlyAtTheMaximum_IsRead()
    {
        var reader = new WebSocketFrameReader(
            new InMemoryConnection([Rfc6455Examples.MaskedTextHello]),
            Rfc6455Examples.MaskedTextHello.Length);

        var result = await reader.ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(WebSocketFrameReadOutcome.FrameRead, result.Outcome);
    }

    [TestMethod]
    public async Task ReadFrameAsync_LengthNoArrayCanHoldAndNoMaximum_IsFrameTooLarge()
    {
        await AssertOutcomeAsync(
            [0x82, 0x7F, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00],
            WebSocketFrameReadOutcome.FrameTooLarge);
    }

    [TestMethod]
    public async Task ReadFrameAsync_CloseWithNoPayload_IsRead()
    {
        var frame = await ReadOneFrameAsync([0x88, 0x00]);

        Assert.AreEqual(WebSocketOpcode.Close, frame.Opcode);
        Assert.IsEmpty(frame.Payload);
    }

    [TestMethod]
    public async Task ReadFrameAsync_CloseWithCodeAndUtf8Reason_IsRead()
    {
        var payload = Rfc6455Examples.Join([0x03, 0xE9], Encoding.UTF8.GetBytes("gone é"));

        var frame = await ReadOneFrameAsync(Rfc6455Examples.Join([0x88, (byte)payload.Length], payload));

        CollectionAssert.AreEqual(payload, frame.Payload);
    }

    [TestMethod]
    public async Task ReadFrameAsync_CloseWithOneBytePayload_IsClosePayloadOneByte()
    {
        await AssertOutcomeAsync([0x88, 0x01, 0x03], WebSocketFrameReadOutcome.ClosePayloadOneByte);
    }

    [TestMethod]
    [DataRow(0x03, 0xED, DisplayName = "1005")]
    [DataRow(0x03, 0xE7, DisplayName = "999")]
    [DataRow(0x0B, 0xB8 - 1, DisplayName = "2999")]
    public async Task ReadFrameAsync_CloseWithACodeNotAllowedOnTheWire_IsCloseCodeNotAllowed(int high, int low)
    {
        await AssertOutcomeAsync([0x88, 0x02, (byte)high, (byte)low], WebSocketFrameReadOutcome.CloseCodeNotAllowed);
    }

    [TestMethod]
    public async Task ReadFrameAsync_CloseWithAReasonThatIsNotUtf8_IsCloseReasonNotUtf8()
    {
        await AssertOutcomeAsync([0x88, 0x04, 0x03, 0xE8, 0xC3, 0x28], WebSocketFrameReadOutcome.CloseReasonNotUtf8);
    }

    private static WebSocketFrameReader ReaderOver(byte[] bytes) => new(new InMemoryConnection([bytes]), 0);

    private async Task<WebSocketFrame> ReadOneFrameAsync(byte[] bytes)
    {
        var result = await ReaderOver(bytes).ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(WebSocketFrameReadOutcome.FrameRead, result.Outcome);
        return result.Frame!;
    }

    private async Task AssertOutcomeAsync(byte[] bytes, WebSocketFrameReadOutcome expected)
    {
        var result = await ReaderOver(bytes).ReadFrameAsync(TestContext.CancellationToken);

        Assert.AreEqual(expected, result.Outcome);
        Assert.IsNull(result.Frame);
    }
}
