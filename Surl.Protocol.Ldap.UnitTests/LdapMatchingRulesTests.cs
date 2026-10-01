using static Surl.Protocol.Ldap.LdapDirectoryFixture;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Picks each attribute's matching rule and puts values in the normal form DNs compare by.
/// </summary>
[TestClass]
public sealed class LdapMatchingRulesTests
{
    [TestMethod]
    [DataRow("userPassword", "OctetString")]
    [DataRow("JPEGPHOTO", "OctetString")]
    [DataRow("userCertificate;binary", "OctetString")]
    [DataRow("cACertificate", "OctetString")]
    [DataRow("photo", "OctetString")]
    [DataRow("audio", "OctetString")]
    [DataRow("description;BINARY", "OctetString")]
    [DataRow("uidNumber", "Integer")]
    [DataRow("gidNumber", "Integer")]
    [DataRow("shadowLastChange", "Integer")]
    [DataRow("shadowMin", "Integer")]
    [DataRow("shadowMax", "Integer")]
    [DataRow("shadowWarning", "Integer")]
    [DataRow("shadowInactive", "Integer")]
    [DataRow("shadowExpire", "Integer")]
    [DataRow("cn;lang-en", "CaseIgnore")]
    [DataRow("2.5.4.3", "CaseIgnore")]
    public void ForDescription_IsTheTypesRule(string description, string expected)
    {
        Assert.AreEqual(expected, LdapMatchingRules.ForDescription(LdapAttributeDescription.Parse(description)).ToString());
    }

    [TestMethod]
    [DataRow("CASEIGNOREMATCH", "CaseIgnore")]
    [DataRow("2.5.13.5", "CaseExact")]
    [DataRow("integerMatch", "Integer")]
    [DataRow("2.5.13.17", "OctetString")]
    public void TryFindByName_FindsTheFourRules(string name, string expected)
    {
        Assert.IsTrue(LdapMatchingRules.TryFindByName(name, out var rule));
        Assert.AreEqual(expected, rule.ToString());
    }

    [TestMethod]
    public void TryFindByName_AnyOtherRule_IsNotFound()
    {
        Assert.IsFalse(LdapMatchingRules.TryFindByName("distinguishedNameMatch", out _));
    }

    [TestMethod]
    public void NormalForm_OfEachRule_IsItsCanonicalText()
    {
        Assert.AreEqual("a b", LdapMatchingRules.NormalForm(LdapMatchingRule.CaseIgnore, Utf8("  A   B ")));
        Assert.AreEqual("A b", LdapMatchingRules.NormalForm(LdapMatchingRule.CaseExact, Utf8("A  b")));
        Assert.AreEqual("-7", LdapMatchingRules.NormalForm(LdapMatchingRule.Integer, Utf8("-007")));
        Assert.AreEqual("78", LdapMatchingRules.NormalForm(LdapMatchingRule.Integer, Utf8("x")));
        Assert.AreEqual("4142", LdapMatchingRules.NormalForm(LdapMatchingRule.OctetString, Utf8("AB")));
        Assert.AreEqual("FF", LdapMatchingRules.NormalForm(LdapMatchingRule.CaseIgnore, [0xFF]));
    }

    [TestMethod]
    public void TryDecodeUtf8_RefusesBytesThatAreNotUtf8()
    {
        Assert.AreEqual("é", LdapMatchingRules.TryDecodeUtf8([0xC3, 0xA9]));
        Assert.IsNull(LdapMatchingRules.TryDecodeUtf8([0xC3]));
    }

    [TestMethod]
    public void Compare_OctetStrings_IsBytewise()
    {
        Assert.AreEqual(-1, LdapMatchingRules.Compare(LdapMatchingRule.OctetString, [0x01], [0x02]));
        Assert.AreEqual(1, LdapMatchingRules.Compare(LdapMatchingRule.OctetString, [0x02, 0x00], [0x02]));
    }
}
