namespace Surl.Protocol.Http;

[TestClass]
public sealed class HttpRequestHeadReadResultTests
{
    [TestMethod]
    public void Read_Head_CarriesItWithHeadRead()
    {
        var head = new HttpRequestHead("GET", "/", new Version(1, 1), []);

        var result = HttpRequestHeadReadResult.Read(head);

        Assert.AreEqual(HttpRequestHeadReadOutcome.HeadRead, result.Outcome);
        Assert.AreSame(head, result.Head);
    }

    [TestMethod]
    public void Read_NullHead_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => HttpRequestHeadReadResult.Read(null!));
    }

    [TestMethod]
    public void NoHead_Failure_CarriesItWithoutAHead()
    {
        var result = HttpRequestHeadReadResult.NoHead(HttpRequestHeadReadOutcome.HeadTooLarge);

        Assert.AreEqual(HttpRequestHeadReadOutcome.HeadTooLarge, result.Outcome);
        Assert.IsNull(result.Head);
    }

    [TestMethod]
    public void NoHead_HeadRead_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => HttpRequestHeadReadResult.NoHead(HttpRequestHeadReadOutcome.HeadRead));
    }
}
