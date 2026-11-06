namespace Surl.Quic;

[TestClass]
public sealed class QuicPeerConnectionIdsTests
{
    private static readonly byte[] Initial = [0x01, 0x02, 0x03, 0x04];

    [TestMethod]
    public void InUse_AtTheStart_IsTheInitialConnectionId()
    {
        var ids = new QuicPeerConnectionIds(Initial, 2);

        CollectionAssert.AreEqual(Initial, ids.InUse.ToArray());
    }

    [TestMethod]
    public void Receive_FromAClientUsingAZeroLengthConnectionId_ThrowsProtocolViolation()
    {
        var ids = new QuicPeerConnectionIds([], 2);

        var exception = Assert.ThrowsExactly<QuicTransportException>(() => ids.Receive(Frame(1, 0, 0xaa)));

        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, exception.ErrorCode);
        Assert.AreEqual((ulong)QuicFrameTypes.NewConnectionId, exception.FrameType);
    }

    [TestMethod]
    public void Receive_AnActiveConnectionIdUnderAnotherSequenceNumber_ThrowsProtocolViolation()
    {
        var ids = new QuicPeerConnectionIds(Initial, 2);

        var exception = Assert.ThrowsExactly<QuicTransportException>(() => ids.Receive(new QuicNewConnectionIdFrame(1, 0, Initial, new byte[16])));

        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, exception.ErrorCode);
    }

    [TestMethod]
    public void Receive_ThirdActiveConnectionId_ThrowsConnectionIdLimitError()
    {
        var ids = new QuicPeerConnectionIds(Initial, 2);
        ids.Receive(Frame(1, 0, 0xaa));

        var exception = Assert.ThrowsExactly<QuicTransportException>(() => ids.Receive(Frame(2, 0, 0xbb)));

        Assert.AreEqual(QuicTransportErrorCode.ConnectionIdLimitError, exception.ErrorCode);
        Assert.AreEqual((ulong)QuicFrameTypes.NewConnectionId, exception.FrameType);
    }

    [TestMethod]
    public void Receive_RetirePriorToCoveringTheIdInUse_RetiresItAndMovesToTheLowestActive()
    {
        var ids = new QuicPeerConnectionIds(Initial, 2);

        var retired = ids.Receive(Frame(1, 1, 0xaa));

        CollectionAssert.AreEqual(new ulong[] { 0 }, retired.ToArray());
        CollectionAssert.AreEqual(Enumerable.Repeat((byte)0xaa, 4).ToArray(), ids.InUse.ToArray());
    }

    [TestMethod]
    public void Receive_SequenceNumberBelowTheRetirePriorToReceived_RetiresItAtOnce()
    {
        var ids = new QuicPeerConnectionIds(Initial, 2);
        ids.Receive(Frame(3, 3, 0xcc));

        var retired = ids.Receive(Frame(2, 0, 0xbb));

        CollectionAssert.AreEqual(new ulong[] { 2 }, retired.ToArray());
        CollectionAssert.AreEqual(Enumerable.Repeat((byte)0xcc, 4).ToArray(), ids.InUse.ToArray());
    }

    [TestMethod]
    public void Receive_LowerRetirePriorToThanOneReceived_RetiresNothingMore()
    {
        var ids = new QuicPeerConnectionIds(Initial, 2);
        ids.Receive(Frame(1, 1, 0xaa));

        var retired = ids.Receive(Frame(2, 0, 0xbb));

        Assert.IsEmpty(retired);
        CollectionAssert.AreEqual(Enumerable.Repeat((byte)0xaa, 4).ToArray(), ids.InUse.ToArray());
    }

    [TestMethod]
    public void Receive_RepeatOfARetiredConnectionId_IsIgnored()
    {
        var ids = new QuicPeerConnectionIds(Initial, 2);
        ids.Receive(Frame(1, 0, 0xaa));
        ids.Receive(Frame(2, 2, 0xbb));

        var retired = ids.Receive(Frame(1, 0, 0xaa));

        Assert.IsEmpty(retired);
        CollectionAssert.AreEqual(Enumerable.Repeat((byte)0xbb, 4).ToArray(), ids.InUse.ToArray());
    }

    private static QuicNewConnectionIdFrame Frame(ulong sequenceNumber, ulong retirePriorTo, byte fill) =>
        new(sequenceNumber, retirePriorTo, Enumerable.Repeat(fill, 4).ToArray(), new byte[QuicNewConnectionIdFrame.StatelessResetTokenLength]);
}
