namespace Surl.Protocol.Ldap;

[TestClass]
public sealed class LdapBindNamesTests
{
    [TestMethod]
    [DataRow("alice", "alice", DisplayName = "A plain name, as curl's -u sends it")]
    [DataRow("al\\=ice", "al\\=ice", DisplayName = "An escaped = only")]
    [DataRow("cn=alice,dc=example,dc=com", "alice", DisplayName = "A cn DN")]
    [DataRow("UID=bob,ou=staff,dc=example,dc=com", "bob", DisplayName = "A uid DN, type in any case")]
    [DataRow("ou=staff,dc=example,dc=com", "ou=staff,dc=example,dc=com", DisplayName = "Another type")]
    [DataRow("cn=alice+sn=Smith,dc=example,dc=com", "cn=alice+sn=Smith,dc=example,dc=com", DisplayName = "A multi-valued RDN")]
    [DataRow("cn=#04056816C6963,dc=com", "cn=#04056816C6963,dc=com", DisplayName = "A BER-encoded value")]
    [DataRow("cn=alice,=", "cn=alice,=", DisplayName = "Not an RFC 4514 DN")]
    public void AccountNameOf_BindName_IsTheAccountChecked(string bindName, string accountName)
    {
        Assert.AreEqual(accountName, LdapBindNames.AccountNameOf(bindName));
    }
}
