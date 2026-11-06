namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>Content headers read and written (section 4.2.6).</summary>
[TestClass]
public sealed class Amqp0ContentHeaderTests
{
    [TestMethod]
    public void ContentHeader_RoundTrips()
    {
        var header = new Amqp0ContentHeader(60, 1_000_000, 0x9000, new byte[] { 4, (byte)'t', (byte)'e', (byte)'x', (byte)'t', 2 });

        var read = Amqp0ContentHeader.Read(header.Encode());

        Assert.AreEqual(60, read.ClassId);
        Assert.AreEqual(1_000_000UL, read.BodySize);
        Assert.AreEqual(0x9000, read.PropertyFlags);
        CollectionAssert.AreEqual(header.PropertyList.ToArray(), read.PropertyList.ToArray());
    }

    [TestMethod]
    public void ContentHeader_NonZeroWeight_IsReadAndDropped()
    {
        byte[] payload = [0, 60, 0, 7, 0, 0, 0, 0, 0, 0, 0, 5, 0, 0];

        var read = Amqp0ContentHeader.Read(payload);

        Assert.AreEqual(5UL, read.BodySize);
        CollectionAssert.AreEqual(new byte[] { 0, 60, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 0, 0 }, read.Encode());
    }

    [TestMethod]
    public void ContentHeader_ContinuationBitSet_IsASyntaxError()
    {
        byte[] payload = [0, 60, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0];

        Assert.ThrowsExactly<Amqp0SyntaxException>(() => Amqp0ContentHeader.Read(payload));
    }

    [TestMethod]
    public void ContentHeader_TooShort_IsASyntaxError()
    {
        Assert.ThrowsExactly<Amqp0SyntaxException>(() => Amqp0ContentHeader.Read(new byte[] { 0, 60, 0, 0, 0 }));
    }

    [TestMethod]
    public void ContentHeader_EncodeWithContinuationBit_Throws()
    {
        var header = new Amqp0ContentHeader(60, 0, 0x0001, ReadOnlyMemory<byte>.Empty);

        Assert.ThrowsExactly<InvalidOperationException>(() => header.Encode());
    }
}
