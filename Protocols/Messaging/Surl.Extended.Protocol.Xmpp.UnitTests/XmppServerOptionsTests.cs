namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// The XMPP server's options (BL-536; ADR-0114 decisions 0 and 1).
/// </summary>
[TestClass]
public sealed class XmppServerOptionsTests
{
    [TestMethod]
    public void Default_HasNoMailDomainsAndTheCryptographicRandomSource()
    {
        var options = XmppServerOptions.Default;

        Assert.IsEmpty(options.MailDomains);
        Assert.AreSame(CryptographicXmppRandomSource.Instance, options.RandomSource);
    }
}
