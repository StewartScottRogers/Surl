namespace Surl.Extended.Protocol.DnsQuery;

[TestClass]
public sealed class DnsCertificationAuthorityAuthorizationDataTests
{
    [TestMethod]
    [DataRow("")]
    [DataRow("abcdefghijklmnop")]
    [DataRow("is-sue")]
    public void Constructor_TagOutsideRfc8659_Throws(string tag)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new DnsCertificationAuthorityAuthorizationData(0, tag, []));
    }

    [TestMethod]
    public void Constructor_FifteenCharacterTag_IsAccepted()
    {
        var caa = new DnsCertificationAuthorityAuthorizationData(128, "abcdefghijklmno", [1]);

        Assert.AreEqual("abcdefghijklmno", caa.Tag);
        Assert.AreEqual(128, caa.Flags);
    }
}
