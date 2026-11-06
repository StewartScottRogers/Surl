namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// The error condition each constructor of <see cref="Amqp1DecodeException"/> gives.
/// </summary>
[TestClass]
public sealed class Amqp1DecodeExceptionTests
{
    [TestMethod]
    public void Constructors_WithoutACondition_GiveDecodeError()
    {
        var inner = new InvalidOperationException();

        Amqp1DecodeException[] exceptions = [new(), new("m"), new("m", inner)];

        Assert.IsTrue(exceptions.All(exception => exception.Condition == Amqp1ErrorCondition.DecodeError));
        Assert.AreSame(inner, exceptions[2].InnerException);
        Assert.AreEqual("m", exceptions[1].Message);
    }

    [TestMethod]
    public void Constructor_WithACondition_GivesIt()
    {
        var exception = new Amqp1DecodeException("m", Amqp1ErrorCondition.FramingError);

        Assert.AreEqual(Amqp1ErrorCondition.FramingError, exception.Condition);
    }
}
