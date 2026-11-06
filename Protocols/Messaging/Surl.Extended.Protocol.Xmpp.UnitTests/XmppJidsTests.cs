namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// The JIDs a login and a bind give a stream (BL-538; ADR-0114 decisions 3 and 4; RFC 7622 section 3).
/// </summary>
[TestClass]
public sealed class XmppJidsTests
{
    [TestMethod]
    [DataRow("user", "user@im.example.com")]
    [DataRow("Juliet", "Juliet@im.example.com")]
    [DataRow("juliet@im.example.com", "juliet@im.example.com")]
    [DataRow("juliet@IM.Example.com.", "juliet@im.example.com")]
    public void BareJid_UsableAccount_IsItsJidOnTheStreamDomain(string accountName, string expected)
    {
        var jid = XmppJids.BareJid(accountName, "im.example.com", new CountingRandomSource());

        Assert.AreEqual(expected, jid);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("a b")]
    [DataRow("a\"b")]
    [DataRow("a&b")]
    [DataRow("a'b")]
    [DataRow("a/b")]
    [DataRow("a:b")]
    [DataRow("a<b")]
    [DataRow("a>b")]
    [DataRow("@im.example.com")]
    [DataRow("juliet@other.example")]
    [DataRow("a@b@im.example.com")]
    public void BareJid_AccountUnusableOnTheStreamDomain_IsNull(string accountName)
    {
        var jid = XmppJids.BareJid(accountName, "im.example.com", new CountingRandomSource());

        Assert.IsNull(jid);
    }

    [TestMethod]
    public void BareJid_LocalpartOverTheByteBound_IsNull()
    {
        var jid = XmppJids.BareJid(new string('é', 512), "im.example.com", new CountingRandomSource());

        Assert.IsNull(jid);
    }

    [TestMethod]
    public void BareJid_NoAccount_IsANewAnonName()
    {
        var jid = XmppJids.BareJid(null, "im.example.com", new CountingRandomSource());

        Assert.AreEqual("anon-0000000000000001@im.example.com", jid);
    }

    [TestMethod]
    [DataRow("balcony", true)]
    [DataRow("a/b c@d", true)]
    [DataRow("", false)]
    [DataRow("bal\u0007cony", false)]
    public void IsValidResourcepart_Resource_IsWhetherItMayBeBound(string resource, bool expected)
    {
        Assert.AreEqual(expected, XmppJids.IsValidResourcepart(resource));
    }

    [TestMethod]
    public void IsValidResourcepart_OverTheByteBound_IsFalse()
    {
        Assert.IsFalse(XmppJids.IsValidResourcepart(new string('a', XmppJids.MaxPartBytes + 1)));
        Assert.IsTrue(XmppJids.IsValidResourcepart(new string('a', XmppJids.MaxPartBytes)));
    }
}
