namespace Surl.Protocol.Http;

[TestClass]
public sealed class HttpRequestFieldTests
{
    [TestMethod]
    public void Constructor_KeepsNameAndValue()
    {
        var field = new HttpRequestField("X-Custom", "a b");

        Assert.AreEqual("X-Custom", field.Name);
        Assert.AreEqual("a b", field.Value);
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpRequestField(null!, "v"));
        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpRequestField("n", null!));
    }
}
