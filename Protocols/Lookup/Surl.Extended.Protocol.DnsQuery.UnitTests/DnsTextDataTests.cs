namespace Surl.Extended.Protocol.DnsQuery;

[TestClass]
public sealed class DnsTextDataTests
{
    [TestMethod]
    public void Constructor_NoString_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new DnsTextData([]));
    }

    [TestMethod]
    public void Constructor_StringOf256Bytes_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new DnsTextData([new byte[256]]));
    }
}
