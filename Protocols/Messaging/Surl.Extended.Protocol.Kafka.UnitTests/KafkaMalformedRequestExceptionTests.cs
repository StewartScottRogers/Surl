namespace Surl.Extended.Protocol.Kafka.UnitTests;

[TestClass]
public sealed class KafkaMalformedRequestExceptionTests
{
    [TestMethod]
    public void Constructors_MessageAndCause_AreKept()
    {
        var cause = new InvalidOperationException("cause");

        var withNothing = new KafkaMalformedRequestException();
        var withMessage = new KafkaMalformedRequestException("m");
        var withCause = new KafkaMalformedRequestException("m", cause);

        Assert.IsNotNull(withNothing.Message);
        Assert.AreEqual("m", withMessage.Message);
        Assert.AreSame(cause, withCause.InnerException);
    }
}
