namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// Frames encoded as section 4.2.3 lays them out.
/// </summary>
[TestClass]
public sealed class Amqp0FrameTests
{
    [TestMethod]
    public void Encode_Heartbeat_IsTypeEightChannelZeroEmptyAndFrameEnd()
    {
        CollectionAssert.AreEqual(new byte[] { 8, 0, 0, 0, 0, 0, 0, 0xCE }, Amqp0Frame.Heartbeat.Encode());
    }

    [TestMethod]
    public void ForMethod_ConnectionStartOkOnChannelZero_IsLaidOutBigEndian()
    {
        var frame = Amqp0Frame.ForMethod(0x0102, new Amqp0MethodPayload(10, 11, new byte[] { 0xAA }));

        CollectionAssert.AreEqual(new byte[] { 1, 0x01, 0x02, 0, 0, 0, 5, 0, 10, 0, 11, 0xAA, 0xCE }, frame.Encode());
    }

    [TestMethod]
    public void ForContentHeader_BasicWithNoProperties_IsLaidOutBigEndian()
    {
        var frame = Amqp0Frame.ForContentHeader(1, new Amqp0ContentHeader(60, 0x0102, 0, ReadOnlyMemory<byte>.Empty));

        CollectionAssert.AreEqual(
            new byte[] { 2, 0, 1, 0, 0, 0, 14, 0, 60, 0, 0, 0, 0, 0, 0, 0, 0, 0x01, 0x02, 0, 0, 0xCE },
            frame.Encode());
    }

    [TestMethod]
    public void ForMethod_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Amqp0Frame.ForMethod(0, null!));
    }

    [TestMethod]
    public void ForContentHeader_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Amqp0Frame.ForContentHeader(0, null!));
    }
}
