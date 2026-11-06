namespace Surl.Control;

[TestClass]
public sealed class ControlRefusalExceptionTests
{
    [TestMethod]
    public void Constructors_WithoutAKind_AreBadRequests()
    {
        var inner = new FormatException("inner");

        ControlRefusalException[] refusals = [new(), new("refused"), new("refused", inner)];

        Assert.IsTrue(refusals.All(refusal => refusal.Kind == ControlRefusalKind.BadRequest));
        Assert.AreEqual("The request was refused.", refusals[0].Message);
        Assert.AreEqual("refused", refusals[1].Message);
        Assert.AreSame(inner, refusals[2].InnerException);
    }

    [TestMethod]
    public void Constructor_WithAKind_KeepsIt()
    {
        var refusal = new ControlRefusalException(ControlRefusalKind.Conflict, "on disk");

        Assert.AreEqual(ControlRefusalKind.Conflict, refusal.Kind);
        Assert.AreEqual("on disk", refusal.Message);
    }
}
