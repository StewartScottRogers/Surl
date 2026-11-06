namespace Surl.Extended.Protocol.Grpc;

[TestClass]
public sealed class LengthPrefixedMessageReaderTests
{
    private const int MessageLimit = 1048576;
    private const long CallLimit = 104857600;

    [TestMethod]
    public void TryRead_MessageSplitAcrossReadsByteByByte_ReassemblesIt()
    {
        byte[] framed = [0x00, 0x00, 0x00, 0x00, 0x03, 0x08, 0x96, 0x01];
        var reader = new LengthPrefixedMessageReader(MessageLimit, CallLimit);
        LengthPrefixedMessage? message = null;

        foreach (var value in framed)
        {
            Assert.IsNull(message);
            reader.Append([value]);
            reader.TryRead(out message);
        }

        Assert.IsNotNull(message);
        Assert.IsFalse(message.IsCompressed);
        CollectionAssert.AreEqual(new byte[] { 0x08, 0x96, 0x01 }, message.Payload.ToArray());
        reader.Complete();
    }

    [TestMethod]
    public void TryRead_TwoMessagesInOneReadAndOneEmpty_ReadsEachInOrder()
    {
        byte[] framed = [0x00, 0x00, 0x00, 0x00, 0x01, 0xAA, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0xBB];
        var reader = new LengthPrefixedMessageReader(MessageLimit, CallLimit);

        reader.Append(framed);

        Assert.IsTrue(reader.TryRead(out var first));
        Assert.IsTrue(reader.TryRead(out var second));
        Assert.IsTrue(reader.TryRead(out var third));
        Assert.IsFalse(reader.TryRead(out _));
        CollectionAssert.AreEqual(new byte[] { 0xAA }, first.Payload.ToArray());
        Assert.IsTrue(second.IsCompressed);
        Assert.AreEqual(0, second.Payload.Length);
        CollectionAssert.AreEqual(new byte[] { 0xBB }, third.Payload.ToArray());
    }

    [TestMethod]
    public void TryRead_MessageLongerThanTheBuffer_IsReadWhole()
    {
        var payload = Enumerable.Range(0, 1000).Select(index => (byte)index).ToArray();
        var framed = LengthPrefixedMessage.WriteUncompressed(payload);
        var reader = new LengthPrefixedMessageReader(MessageLimit, CallLimit);

        reader.Append(framed.AsSpan(0, 200));
        Assert.IsFalse(reader.TryRead(out _));
        reader.Append(framed.AsSpan(200));

        Assert.IsTrue(reader.TryRead(out var message));
        CollectionAssert.AreEqual(payload, message.Payload.ToArray());
    }

    [TestMethod]
    public void TryRead_ManySmallMessagesFillingTheBuffer_MovesTheUnreadBytesToTheFront()
    {
        var framed = LengthPrefixedMessage.WriteUncompressed(new byte[100]);
        var reader = new LengthPrefixedMessageReader(MessageLimit, CallLimit);
        var read = 0;

        for (var round = 0; round < 10; round++)
        {
            reader.Append(framed.AsSpan(0, 60));
            reader.Append(framed.AsSpan(60));
            while (reader.TryRead(out _))
            {
                read++;
            }
        }

        Assert.AreEqual(10, read);
        reader.Complete();
    }

    [TestMethod]
    public void TryRead_CompressedFlagOtherThanZeroOrOne_ThrowsInternal()
    {
        var reader = new LengthPrefixedMessageReader(MessageLimit, CallLimit);
        reader.Append([0x02, 0x00, 0x00, 0x00, 0x00]);

        var thrown = Assert.ThrowsExactly<GrpcCallException>(() => reader.TryRead(out _));

        Assert.AreEqual(GrpcStatus.RequestMessageDidNotParse, thrown.Status);
    }

    [TestMethod]
    public void TryRead_MessageLengthOverTheLimit_ThrowsResourceExhaustedFromThePrefixAlone()
    {
        var reader = new LengthPrefixedMessageReader(16, CallLimit);
        reader.Append([0x00, 0x00, 0x00, 0x00, 0x11]);

        var thrown = Assert.ThrowsExactly<GrpcCallException>(() => reader.TryRead(out _));

        Assert.AreEqual(GrpcStatusCode.ResourceExhausted, thrown.Status.Code);
        Assert.AreEqual("message larger than the limit", thrown.Status.Message);
    }

    [TestMethod]
    public void TryRead_MessageLengthAtTheLimit_IsRead()
    {
        var reader = new LengthPrefixedMessageReader(16, CallLimit);
        reader.Append(LengthPrefixedMessage.WriteUncompressed(new byte[16]));

        Assert.IsTrue(reader.TryRead(out _));
    }

    [TestMethod]
    public void TryRead_MessageLengthOver4GiBMinus1_ThrowsResourceExhaustedWithTheLargestLimit()
    {
        var reader = new LengthPrefixedMessageReader(int.MaxValue, long.MaxValue);
        reader.Append([0x00, 0xFF, 0xFF, 0xFF, 0xFF]);

        var thrown = Assert.ThrowsExactly<GrpcCallException>(() => reader.TryRead(out _));

        Assert.AreEqual(GrpcStatus.MessageLargerThanTheLimit, thrown.Status);
    }

    [TestMethod]
    public void TryRead_MessagesTogetherOverTheCallLimit_ThrowsResourceExhausted()
    {
        var reader = new LengthPrefixedMessageReader(10, 15);
        reader.Append(LengthPrefixedMessage.WriteUncompressed(new byte[10]));
        reader.Append(LengthPrefixedMessage.WriteUncompressed(new byte[6]));
        Assert.IsTrue(reader.TryRead(out _));

        var thrown = Assert.ThrowsExactly<GrpcCallException>(() => reader.TryRead(out _));

        Assert.AreEqual(GrpcStatus.CallLargerThanTheLimit, thrown.Status);
    }

    [TestMethod]
    public void Complete_BodyEndedInsideAMessage_ThrowsInternal()
    {
        var reader = new LengthPrefixedMessageReader(MessageLimit, CallLimit);
        reader.Append([0x00, 0x00, 0x00]);

        var thrown = Assert.ThrowsExactly<GrpcCallException>(reader.Complete);

        Assert.AreEqual(GrpcStatus.RequestMessageDidNotParse, thrown.Status);
    }

    [TestMethod]
    public void Constructor_NegativeLimit_ThrowsArgumentOutOfRangeException()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LengthPrefixedMessageReader(-1, CallLimit));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LengthPrefixedMessageReader(MessageLimit, -1));
    }
}
