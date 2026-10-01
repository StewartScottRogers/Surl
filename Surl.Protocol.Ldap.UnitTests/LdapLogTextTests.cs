namespace Surl.Protocol.Ldap;

[TestClass]
public sealed class LdapLogTextTests
{
    [TestMethod]
    [DataRow("dc=example,dc=com", "dc=example,dc=com", DisplayName = "Printable ASCII")]
    [DataRow("a\\b", "a\\x5Cb", DisplayName = "A backslash")]
    [DataRow("a\r\nb", "a\\x0D\\x0Ab", DisplayName = "Control characters")]
    [DataRow("café", "caf\\xC3\\xA9", DisplayName = "Non-ASCII, as UTF-8")]
    public void Render_PeerText_IsEscaped(string text, string rendered)
    {
        Assert.AreEqual(rendered, LdapLogText.Render(text));
    }

    [TestMethod]
    [DataRow((int)LdapResultCode.Success, "success")]
    [DataRow((int)LdapResultCode.ConfidentialityRequired, "confidentialityRequired")]
    [DataRow((int)LdapResultCode.InsufficientAccessRights, "insufficientAccessRights")]
    public void NameOf_ResultCode_IsItsRfc4511Name(int code, string name)
    {
        Assert.AreEqual(name, LdapLogText.NameOf((LdapResultCode)code));
    }
}
