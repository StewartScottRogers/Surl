namespace Surl.Extended.Protocol.DnsQuery;

[TestClass]
public sealed class DnsOptionTests
{
    [TestMethod]
    public void Constructor_DataOf65536Bytes_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new DnsOption(DnsOption.PaddingCode, new byte[65536]));
    }
}
