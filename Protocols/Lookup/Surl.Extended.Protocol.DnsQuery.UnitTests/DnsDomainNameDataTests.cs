namespace Surl.Extended.Protocol.DnsQuery;

[TestClass]
public sealed class DnsDomainNameDataTests
{
    [TestMethod]
    [DataRow(DnsRecordType.A)]
    [DataRow(DnsRecordType.MX)]
    public void Constructor_TypeWhoseRdataIsNotOneName_Throws(DnsRecordType type)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new DnsDomainNameData(type, DnsName.Root));
    }
}
