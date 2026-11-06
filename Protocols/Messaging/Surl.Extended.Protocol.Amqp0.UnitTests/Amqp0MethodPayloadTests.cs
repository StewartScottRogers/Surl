namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>Method payloads read and written (section 4.2.4).</summary>
[TestClass]
public sealed class Amqp0MethodPayloadTests
{
    [TestMethod]
    public void MethodPayload_RoundTrips()
    {
        var method = new Amqp0MethodPayload(60, 40, new byte[] { 0, 0, 3, (byte)'k', (byte)'e', (byte)'y' });

        var read = Amqp0MethodPayload.Read(method.Encode());

        Assert.AreEqual(60, read.ClassId);
        Assert.AreEqual(40, read.MethodId);
        CollectionAssert.AreEqual(method.Arguments.ToArray(), read.Arguments.ToArray());
    }

    [TestMethod]
    public void MethodPayload_ShorterThanItsIds_IsASyntaxError()
    {
        var exception = Assert.ThrowsExactly<Amqp0SyntaxException>(() => Amqp0MethodPayload.Read(new byte[] { 0, 10, 0 }));

        Assert.AreEqual(Amqp0ReplyCode.SyntaxError, exception.ReplyCode);
    }
}
