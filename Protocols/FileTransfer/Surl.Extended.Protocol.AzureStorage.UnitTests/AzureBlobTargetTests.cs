namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// How a request target splits into container, blob name and query (ADR-0109, decision 2).
/// </summary>
[TestClass]
public sealed class AzureBlobTargetTests
{
    [TestMethod]
    public void Parse_TargetWithoutALeadingSlash_NamesNoContainer()
    {
        var target = AzureBlobTarget.Parse("*");

        Assert.AreEqual(string.Empty, target.RawContainer);
        Assert.IsFalse(target.NamesABlob);
    }

    [TestMethod]
    public void Parse_QueryWithAFlagAndAPlus_DecodesEachParameter()
    {
        var target = AzureBlobTarget.Parse("/c/a%2Fb?flag&prefix=a+b%21&");

        Assert.AreEqual("a/b", target.BlobName);
        CollectionAssert.AreEqual(
            new[] { new KeyValuePair<string, string>("flag", string.Empty), new KeyValuePair<string, string>("prefix", "a b!") },
            target.QueryParameters.ToArray());
    }
}
