namespace Surl.Authentication;

/// <summary>
/// <see cref="LdapDirectoryUrl.TryParse"/> against RFC 4516 and ADR-0149 decision 3.1.
/// </summary>
[TestClass]
public sealed class LdapDirectoryUrlTests
{
    [TestMethod]
    public void TryParse_BareUrl_TakesTheDefaults()
    {
        var isParsed = LdapDirectoryUrl.TryParse("ldap://dir.test/dc=example,dc=com", out var url, out _);

        Assert.IsTrue(isParsed);
        Assert.IsFalse(url!.UsesTlsFromTheStart);
        Assert.AreEqual("dir.test", url.Host);
        Assert.AreEqual(389, url.Port);
        Assert.AreEqual("dc=example,dc=com", url.BaseDn);
        Assert.AreEqual("uid", url.Attribute);
        Assert.AreEqual(LdapSearchScope.Subtree, url.Scope);
        CollectionAssert.AreEqual(LdapFilterEncoder.TryEncode(LdapDirectoryUrl.DefaultFilter), url.EncodedFilter);
    }

    [TestMethod]
    public void TryParse_EverythingNamed_IsRead()
    {
        var isParsed = LdapDirectoryUrl.TryParse(
            "LDAPS://[::1]:6360/dc=x%2Cdc=y?sAMAccountName?one?(memberOf=cn%3Dt)", out var url, out _);

        Assert.IsTrue(isParsed);
        Assert.IsTrue(url!.UsesTlsFromTheStart);
        Assert.AreEqual("::1", url.Host);
        Assert.AreEqual(6360, url.Port);
        Assert.AreEqual("dc=x,dc=y", url.BaseDn);
        Assert.AreEqual("sAMAccountName", url.Attribute);
        Assert.AreEqual(LdapSearchScope.OneLevel, url.Scope);
        CollectionAssert.AreEqual(LdapFilterEncoder.TryEncode("(memberOf=cn=t)"), url.EncodedFilter);
    }

    [TestMethod]
    [DataRow("ldaps://h/dc=a", "h", 636, DisplayName = "ldaps default port")]
    [DataRow("ldap://[::1]/dc=a", "::1", 389, DisplayName = "an IPv6 literal without a port")]
    [DataRow("ldap://h:65535/dc=a", "h", 65535, DisplayName = "the highest port")]
    [DataRow("ldap://h/dc=a????", "h", 389, DisplayName = "empty extensions")]
    public void TryParse_HostAndPort_AreRead(string text, string host, int port)
    {
        Assert.IsTrue(LdapDirectoryUrl.TryParse(text, out var url, out _));
        Assert.AreEqual(host, url!.Host);
        Assert.AreEqual(port, url.Port);
    }

    [TestMethod]
    [DataRow("base", LdapSearchScope.Base)]
    [DataRow("ONE", LdapSearchScope.OneLevel)]
    [DataRow("sub", LdapSearchScope.Subtree)]
    [DataRow("", LdapSearchScope.Subtree)]
    public void TryParse_Scope_IsRead(string scope, LdapSearchScope expected)
    {
        Assert.IsTrue(LdapDirectoryUrl.TryParse($"ldap://h/dc=a??{scope}", out var url, out _));
        Assert.AreEqual(expected, url!.Scope);
    }

    [TestMethod]
    [DataRow("cn=a\\,b,dc=x")]
    [DataRow("cn=a+sn=b,dc=x")]
    [DataRow("cn=a;dc=x")]
    public void TryParse_DnForms_AreAccepted(string dn)
    {
        Assert.IsTrue(LdapDirectoryUrl.TryParse("ldap://h/" + dn, out _, out _));
    }

    [TestMethod]
    [DataRow("http://h/dc=a", LdapDirectoryUrlFailure.NotAnLdapUrl)]
    [DataRow("ldap://u:p@h/dc=a", LdapDirectoryUrlFailure.HasUserInformation)]
    [DataRow("ldap:///dc=a", LdapDirectoryUrlFailure.NoHost)]
    [DataRow("ldap://:389/dc=a", LdapDirectoryUrlFailure.NoHost)]
    [DataRow("ldap://h:0/dc=a", LdapDirectoryUrlFailure.BadPort)]
    [DataRow("ldap://h:65536/dc=a", LdapDirectoryUrlFailure.BadPort)]
    [DataRow("ldap://h:x/dc=a", LdapDirectoryUrlFailure.BadPort)]
    [DataRow("ldap://h:/dc=a", LdapDirectoryUrlFailure.BadPort)]
    [DataRow("ldap://h", LdapDirectoryUrlFailure.BadBaseDn)]
    [DataRow("ldap://h/", LdapDirectoryUrlFailure.BadBaseDn)]
    [DataRow("ldap://h/nodn", LdapDirectoryUrlFailure.BadBaseDn)]
    [DataRow("ldap://h/dc=a,", LdapDirectoryUrlFailure.BadBaseDn)]
    [DataRow("ldap://h/dc=a%5C", LdapDirectoryUrlFailure.BadBaseDn)]
    [DataRow("ldap://h/dc=a?uid,cn", LdapDirectoryUrlFailure.MultipleAttributes)]
    [DataRow("ldap://h/dc=a?bad%20attr", LdapDirectoryUrlFailure.BadAttribute)]
    [DataRow("ldap://h/dc=a??bogus", LdapDirectoryUrlFailure.BadScope)]
    [DataRow("ldap://h/dc=a???(bad", LdapDirectoryUrlFailure.BadFilter)]
    [DataRow("ldap://h/dc=a????x=y", LdapDirectoryUrlFailure.HasExtensions)]
    [DataRow("ldap://h/dc=a????!x=y", LdapDirectoryUrlFailure.HasExtensions)]
    public void TryParse_Malformed_NamesWhy(string text, LdapDirectoryUrlFailure expected)
    {
        var isParsed = LdapDirectoryUrl.TryParse(text, out var url, out var failure);

        Assert.IsFalse(isParsed);
        Assert.IsNull(url);
        Assert.AreEqual(expected, failure);
    }

    [TestMethod]
    public void TryParse_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => LdapDirectoryUrl.TryParse(null!, out _, out _));
    }
}
