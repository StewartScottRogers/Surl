using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0TestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary><c>connection.close</c> both ways (ADR-0112 decision 7).</summary>
[TestClass]
public sealed class Amqp0ConnectionCloseTests
{
    private static readonly byte[] Arguments = [.. Short(530), .. ShortString("NOT_ALLOWED - x"), .. Short(10), .. Short(40)];

    [TestMethod]
    public void Read_Arguments_ReadsEachField() =>
        Assert.AreEqual(new Amqp0ConnectionClose(530, "NOT_ALLOWED - x", 10, 40), Amqp0ConnectionClose.Read(Arguments));

    [TestMethod]
    public void ToMethod_IsClassTenMethodFiftyWithTheFields()
    {
        var method = new Amqp0ConnectionClose(530, "NOT_ALLOWED - x", 10, 40).ToMethod();

        Assert.AreEqual((ushort)10, method.ClassId);
        Assert.AreEqual((ushort)50, method.MethodId);
        CollectionAssert.AreEqual(Arguments, method.Arguments.ToArray());
    }
}
