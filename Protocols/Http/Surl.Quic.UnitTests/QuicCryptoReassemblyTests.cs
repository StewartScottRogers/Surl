namespace Surl.Quic;

/// <summary>Puts CRYPTO data back in order, as ngtcp2's shuffled and repeated frames need (ADR-0082, Measured).</summary>
[TestClass]
public sealed class QuicCryptoReassemblyTests
{
    [TestMethod]
    public void Add_FramesOutOfOrderOverlappingAndRepeated_DeliversEachByteOnceInOrder()
    {
        var reassembly = new QuicCryptoReassembly();

        var first = reassembly.Add(4, [4, 5, 6]);
        var second = reassembly.Add(2, [2, 3, 4]);
        var third = reassembly.Add(0, [0, 1, 2]);
        var repeated = reassembly.Add(1, [1, 2]);
        var overlapping = reassembly.Add(5, [5, 6, 7]);

        Assert.IsEmpty(first);
        Assert.IsEmpty(second);
        CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6 }, third);
        Assert.IsEmpty(repeated);
        CollectionAssert.AreEqual(new byte[] { 7 }, overlapping);
    }

    [TestMethod]
    public void Add_ShorterFrameAtAnOffsetAlreadyBuffered_KeepsTheLongerOne()
    {
        var reassembly = new QuicCryptoReassembly();
        reassembly.Add(2, [2, 3, 4]);
        reassembly.Add(2, [2]);
        reassembly.Add(3, []);

        var delivered = reassembly.Add(0, [0, 1]);

        CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, delivered);
    }

    [TestMethod]
    public void Add_DataReachingPastTheBuffer_IsCryptoBufferExceeded()
    {
        var reassembly = new QuicCryptoReassembly();

        var exception = Assert.ThrowsExactly<QuicTransportException>(() => reassembly.Add(QuicCryptoReassembly.MaxBufferedBytes, [0]));

        Assert.AreEqual(QuicTransportErrorCode.CryptoBufferExceeded, exception.ErrorCode);
        Assert.AreEqual((ulong)0x06, exception.FrameType);
        Assert.IsEmpty(reassembly.Add(QuicCryptoReassembly.MaxBufferedBytes - 1, [0]));
    }
}
