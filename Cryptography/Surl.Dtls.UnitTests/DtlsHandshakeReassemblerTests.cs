namespace Surl.Dtls;

[TestClass]
public sealed class DtlsHandshakeReassemblerTests
{
    private const int Limit = 1000;

    private static readonly byte[] CertificateBody = Enumerable.Range(0, 30).Select(value => (byte)value).ToArray();

    private static IReadOnlyList<byte[]> ThreeFragments(ushort messageSeq = 0) =>
        DtlsHandshakeFragmenter.Fragment(
            new DtlsHandshakeMessage(DtlsHandshakeType.Certificate, messageSeq, CertificateBody), DtlsHandshakeHeader.Size + 10);

    [TestMethod]
    public void Accept_ThreeFragmentsOutOfOrder_ReassemblesTheMessage()
    {
        var fragments = ThreeFragments();
        var reassembler = new DtlsHandshakeReassembler(Limit);

        Assert.AreEqual(DtlsHandshakeFragmentOutcome.Buffered, reassembler.Accept(fragments[2], out var consumed));
        Assert.AreEqual(fragments[2].Length, consumed);
        Assert.IsFalse(reassembler.TryTakeNext(out _));
        Assert.AreEqual(DtlsHandshakeFragmentOutcome.Buffered, reassembler.Accept(fragments[0], out _));
        Assert.AreEqual(DtlsHandshakeFragmentOutcome.Completed, reassembler.Accept(fragments[1], out _));

        Assert.IsTrue(reassembler.TryTakeNext(out var message));
        Assert.AreEqual(DtlsHandshakeType.Certificate, message.MessageType);
        Assert.AreEqual((ushort)0, message.MessageSeq);
        CollectionAssert.AreEqual(CertificateBody, message.Body);
        Assert.AreEqual((ushort)1, reassembler.NextMessageSeq);
    }

    [TestMethod]
    public void Accept_DuplicateFragment_IsIgnored()
    {
        var fragments = ThreeFragments();
        var reassembler = new DtlsHandshakeReassembler(Limit);
        reassembler.Accept(fragments[1], out _);

        var outcome = reassembler.Accept(fragments[1], out var consumed);

        Assert.AreEqual(DtlsHandshakeFragmentOutcome.Duplicate, outcome);
        Assert.AreEqual(fragments[1].Length, consumed);
    }

    [TestMethod]
    public void Accept_OverlappingFragment_KeepsOnlyTheNewBytes()
    {
        var reassembler = new DtlsHandshakeReassembler(Limit);
        var message = new DtlsHandshakeMessage(DtlsHandshakeType.Certificate, 0, CertificateBody);
        var wholeAsOne = message.ToUnfragmentedBytes();
        reassembler.Accept(ThreeFragments()[0], out _);

        var outcome = reassembler.Accept(wholeAsOne, out _);

        Assert.AreEqual(DtlsHandshakeFragmentOutcome.Completed, outcome);
        Assert.IsTrue(reassembler.TryTakeNext(out var taken));
        CollectionAssert.AreEqual(CertificateBody, taken.Body);
    }

    [TestMethod]
    public void Accept_FragmentOfAMessageAlreadyHandedOver_IsStale()
    {
        var reassembler = new DtlsHandshakeReassembler(Limit, nextMessageSeq: 1);

        var outcome = reassembler.Accept(ThreeFragments(messageSeq: 0)[0], out _);

        Assert.AreEqual(DtlsHandshakeFragmentOutcome.Stale, outcome);
    }

    [TestMethod]
    public void Accept_MessageLongerThanTheLimit_IsOverLimit()
    {
        var reassembler = new DtlsHandshakeReassembler(CertificateBody.Length - 1);

        var outcome = reassembler.Accept(ThreeFragments()[0], out _);

        Assert.AreEqual(DtlsHandshakeFragmentOutcome.OverLimit, outcome);
    }

    [TestMethod]
    public void Accept_TwoMessagesTogetherPastTheLimit_RefusesTheSecondUntilTheFirstIsTaken()
    {
        var reassembler = new DtlsHandshakeReassembler(CertificateBody.Length + 10);
        reassembler.Accept(new DtlsHandshakeMessage(DtlsHandshakeType.Certificate, 0, CertificateBody).ToUnfragmentedBytes(), out _);
        var second = ThreeFragments(messageSeq: 1)[0];

        Assert.AreEqual(DtlsHandshakeFragmentOutcome.OverLimit, reassembler.Accept(second, out _));
        Assert.IsTrue(reassembler.TryTakeNext(out _));
        Assert.AreEqual(DtlsHandshakeFragmentOutcome.Buffered, reassembler.Accept(second, out _));
    }

    [TestMethod]
    public void Accept_LaterMessageFirst_IsHandedOverOnlyAfterTheEarlierOne()
    {
        var reassembler = new DtlsHandshakeReassembler(Limit);
        var done = new DtlsHandshakeMessage(DtlsHandshakeType.ServerHelloDone, 1, []).ToUnfragmentedBytes();
        var finished = new DtlsHandshakeMessage(DtlsHandshakeType.Finished, 0, new byte[12]).ToUnfragmentedBytes();

        Assert.AreEqual(DtlsHandshakeFragmentOutcome.Completed, reassembler.Accept(done, out _));
        Assert.IsFalse(reassembler.TryTakeNext(out _));
        Assert.AreEqual(DtlsHandshakeFragmentOutcome.Completed, reassembler.Accept(finished, out _));

        Assert.IsTrue(reassembler.TryTakeNext(out var first));
        Assert.IsTrue(reassembler.TryTakeNext(out var second));
        Assert.AreEqual(DtlsHandshakeType.Finished, first.MessageType);
        Assert.AreEqual(DtlsHandshakeType.ServerHelloDone, second.MessageType);
        Assert.IsEmpty(second.Body);
    }

    [TestMethod]
    public void Accept_EmptyMessageTwice_IsADuplicate()
    {
        var reassembler = new DtlsHandshakeReassembler(Limit);
        var done = new DtlsHandshakeMessage(DtlsHandshakeType.ServerHelloDone, 0, []).ToUnfragmentedBytes();
        reassembler.Accept(done, out _);

        Assert.AreEqual(DtlsHandshakeFragmentOutcome.Duplicate, reassembler.Accept(done, out _));
    }

    [TestMethod]
    [DataRow(DtlsHandshakeType.ClientKeyExchange, 30)]
    [DataRow(DtlsHandshakeType.Certificate, 31)]
    public void Accept_FragmentDisagreeingAboutTypeOrLength_IsContradictory(DtlsHandshakeType messageType, int length)
    {
        var reassembler = new DtlsHandshakeReassembler(Limit);
        reassembler.Accept(ThreeFragments()[0], out _);
        var contradicting = new byte[DtlsHandshakeHeader.Size + 1];
        new DtlsHandshakeHeader(messageType, length, 0, 15, 1).Write(contradicting);

        var outcome = reassembler.Accept(contradicting, out _);

        Assert.AreEqual(DtlsHandshakeFragmentOutcome.Contradictory, outcome);
    }

    [TestMethod]
    public void Accept_FewerThanAHeader_IsMalformedAndTakesEverything()
    {
        var reassembler = new DtlsHandshakeReassembler(Limit);

        var outcome = reassembler.Accept(new byte[11], out var consumed);

        Assert.AreEqual(DtlsHandshakeFragmentOutcome.Malformed, outcome);
        Assert.AreEqual(11, consumed);
    }

    [TestMethod]
    public void Accept_FragmentLengthPastTheRecord_IsMalformed()
    {
        var reassembler = new DtlsHandshakeReassembler(Limit);
        var truncated = ThreeFragments()[0].AsSpan(0, DtlsHandshakeHeader.Size + 5).ToArray();

        Assert.AreEqual(DtlsHandshakeFragmentOutcome.Malformed, reassembler.Accept(truncated, out _));
    }

    [TestMethod]
    public void Accept_FragmentPastItsMessagesLength_IsMalformed()
    {
        var reassembler = new DtlsHandshakeReassembler(Limit);
        var overrun = new byte[DtlsHandshakeHeader.Size + 4];
        new DtlsHandshakeHeader(DtlsHandshakeType.Certificate, 10, 0, 8, 4).Write(overrun);

        Assert.AreEqual(DtlsHandshakeFragmentOutcome.Malformed, reassembler.Accept(overrun, out _));
    }

    [TestMethod]
    public void Accept_TwoFragmentsInOneRecord_ConsumesOneAtATime()
    {
        var fragments = ThreeFragments();
        byte[] record = [.. fragments[0], .. fragments[1]];
        var reassembler = new DtlsHandshakeReassembler(Limit);

        reassembler.Accept(record, out var consumed);
        var outcome = reassembler.Accept(record.AsSpan(consumed), out var consumedSecond);

        Assert.AreEqual(fragments[0].Length, consumed);
        Assert.AreEqual(fragments[1].Length, consumedSecond);
        Assert.AreEqual(DtlsHandshakeFragmentOutcome.Buffered, outcome);
    }

    [TestMethod]
    public void Constructor_NegativeLimit_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DtlsHandshakeReassembler(-1));
    }
}
