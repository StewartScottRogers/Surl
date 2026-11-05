namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseFormatExceptionTests
{
    [TestMethod]
    public void Constructor_LineAndProblem_AreInTheMessage()
    {
        var exception = new UpstreamTestCaseFormatException(7, "bad");

        Assert.AreEqual("line 7: bad", exception.Message);
        Assert.AreEqual(7, exception.LineNumber);
        Assert.AreEqual("bad", exception.Problem);
        Assert.IsNull(exception.InnerException);
    }

    [TestMethod]
    public void Constructor_InnerException_IsKept()
    {
        var inner = new IOException("disk");

        var exception = new UpstreamTestCaseFormatException(1, "disk", inner);

        Assert.AreSame(inner, exception.InnerException);
    }
}
