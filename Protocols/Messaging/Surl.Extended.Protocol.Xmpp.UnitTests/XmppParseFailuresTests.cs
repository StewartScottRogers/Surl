namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// How a parse failure is named (BL-536; RFC 6120 sections 4.9.3 and 11; ADR-0114 decision 6).
/// </summary>
[TestClass]
public sealed class XmppParseFailuresTests
{
    [TestMethod]
    [DataRow("<!DOCTYPE stream>", XmppStreamErrors.RestrictedXml)]
    [DataRow("<body>&ent;</body>", XmppStreamErrors.RestrictedXml)]
    [DataRow("<body>&lt;&_x;</body>", XmppStreamErrors.RestrictedXml)]
    [DataRow("<body>&:x;</body>", XmppStreamErrors.RestrictedXml)]
    [DataRow("<body>&amp;&lt;&gt;&quot;&apos;&#65;&#x41;</a>", XmppStreamErrors.NotWellFormed)]
    [DataRow("<body>&;</a>", XmppStreamErrors.NotWellFormed)]
    [DataRow("<body>& b;</a>", XmppStreamErrors.NotWellFormed)]
    [DataRow("<body>&a b;</a>", XmppStreamErrors.NotWellFormed)]
    [DataRow("<body>&a<b;</a>", XmppStreamErrors.NotWellFormed)]
    [DataRow("<body>&a&b</a>", XmppStreamErrors.NotWellFormed)]
    [DataRow("<?xml version='1.0' encoding='UTF-16'?><stream:stream>", XmppStreamErrors.UnsupportedEncoding)]
    [DataRow("<?xml version=\"1.0\" encoding = \"ISO-8859-1\" ?>", XmppStreamErrors.UnsupportedEncoding)]
    [DataRow("<?xml version='1.0' encoding='utf-8'?><a></b>", XmppStreamErrors.NotWellFormed)]
    [DataRow("<?xml version='1.0'?><a></b>", XmppStreamErrors.NotWellFormed)]
    [DataRow("<?xml version='1.0' encoding='UTF-16'", XmppStreamErrors.NotWellFormed)]
    [DataRow("<a></b>", XmppStreamErrors.NotWellFormed)]
    public void Condition_RecentText_NamesTheFailure(string recentText, string condition)
    {
        Assert.AreEqual(condition, XmppParseFailures.Condition(recentText));
    }
}
