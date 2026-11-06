namespace Surl.McpServing;

[TestClass]
public sealed class McpToolFailureExceptionTests
{
    [TestMethod]
    public void Constructors_MessageAndInnerException_AreKept()
    {
        InvalidOperationException inner = new("inner");

        Assert.IsNotNull(new McpToolFailureException().Message);
        Assert.AreEqual("m", new McpToolFailureException("m").Message);
        Assert.AreSame(inner, new McpToolFailureException("m", inner).InnerException);
    }
}
