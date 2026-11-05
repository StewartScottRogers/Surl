namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The order List Objects returns names in.
/// </summary>
[TestClass]
public sealed class GcsNameOrderTests
{
    [TestMethod]
    public void Compare_NullNames_SortAsEmpty()
    {
        Assert.AreEqual(0, GcsNameOrder.Instance.Compare(null, string.Empty));
        Assert.IsLessThan(0, GcsNameOrder.Instance.Compare(null, "a"));
        Assert.IsGreaterThan(0, GcsNameOrder.Instance.Compare("a", null));
    }
}
