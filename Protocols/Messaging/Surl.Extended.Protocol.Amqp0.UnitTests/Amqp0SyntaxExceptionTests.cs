namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// The syntax exception's constructors and its reply code.
/// </summary>
[TestClass]
public sealed class Amqp0SyntaxExceptionTests
{
    [TestMethod]
    public void Constructors_KeepMessageAndCause()
    {
        var cause = new InvalidOperationException();

        var bare = new Amqp0SyntaxException();
        var withMessage = new Amqp0SyntaxException("short");
        var withCause = new Amqp0SyntaxException("short", cause);

        Assert.AreEqual(Amqp0ReplyCode.SyntaxError, bare.ReplyCode);
        Assert.AreEqual("short", withMessage.Message);
        Assert.AreSame(cause, withCause.InnerException);
    }
}
