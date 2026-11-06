namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Reading a query parameter.
/// </summary>
[TestClass]
public sealed class OciQueryParametersTests
{
    [TestMethod]
    [DataRow("n=2&last=a", "last", "a")]
    [DataRow("flag&n=1", "flag", "")]
    [DataRow("t=a+b%2Bc&t=other", "t", "a+b+c")]
    [DataRow("&&n=3", "n", "3")]
    public void First_NamedParameter_IsItsDecodedValue(string query, string name, string expected)
    {
        Assert.AreEqual(expected, OciQueryParameters.First(query, name));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("m=1&nn=2")]
    public void First_NoSuchParameter_IsNull(string query)
    {
        Assert.IsNull(OciQueryParameters.First(query, "n"));
    }

    [TestMethod]
    public void All_RepeatedParameter_IsEveryDecodedValueInOrder()
    {
        var values = OciQueryParameters.All("scope=a%3Ab&n=1&scope&scope=c", "scope").ToArray();

        CollectionAssert.AreEqual(new[] { "a:b", string.Empty, "c" }, values);
    }
}
