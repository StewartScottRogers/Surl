namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// The stream-level text the server writes (BL-536; RFC 6120 sections 4.7 and 4.9.2).
/// </summary>
[TestClass]
public sealed class XmppStreamTextTests
{
    [TestMethod]
    public void ResponseHeader_ValuesNeedingEscapes_AreEscaped()
    {
        var header = XmppStreamText.ResponseHeader("a'b", "<id>", "en\"&");

        Assert.AreEqual(
            "<?xml version='1.0'?><stream:stream from='a&apos;b' id='&lt;id&gt;' version='1.0' xml:lang='en&quot;&amp;' " +
            "xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'>",
            header);
    }

    [TestMethod]
    public void ErrorAndClose_Condition_IsTheConditionInTheStreamErrorsNamespaceWithNoText()
    {
        Assert.AreEqual(
            "<stream:error><policy-violation xmlns='urn:ietf:params:xml:ns:xmpp-streams'/></stream:error></stream:stream>",
            XmppStreamText.ErrorAndClose(XmppStreamErrors.PolicyViolation));
    }
}
