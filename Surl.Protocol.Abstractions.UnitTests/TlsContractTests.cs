using System.Net.Security;
using System.Security.Authentication;

namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class TlsContractTests
{
    [TestMethod]
    [DataRow("https")]
    [DataRow("wss")]
    [DataRow("ftps")]
    [DataRow("imaps")]
    [DataRow("pop3s")]
    [DataRow("smtps")]
    [DataRow("ldaps")]
    [DataRow("gophers")]
    [DataRow("mqtts")]
    [DataRow("smbs")]
    [DataRow("HTTPS")]
    public void IsImplicitTls_ImplicitTlsScheme_IsTrue(string scheme)
    {
        Assert.IsTrue(TlsSchemes.IsImplicitTls(scheme));
    }

    [TestMethod]
    [DataRow("http")]
    [DataRow("ws")]
    [DataRow("ftp")]
    [DataRow("imap")]
    [DataRow("pop3")]
    [DataRow("smtp")]
    [DataRow("ldap")]
    [DataRow("gopher")]
    [DataRow("mqtt")]
    [DataRow("smb")]
    [DataRow("tftp")]
    [DataRow("")]
    public void IsImplicitTls_PlaintextOrUpgradeScheme_IsFalse(string scheme)
    {
        Assert.IsFalse(TlsSchemes.IsImplicitTls(scheme));
    }

    [TestMethod]
    public void IsImplicitTls_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsSchemes.IsImplicitTls(null!));
    }

    [TestMethod]
    public void TlsHandshakeException_KeepsMessageAndInnerException_AndIsAnIOException()
    {
        var inner = new AuthenticationException("alert");

        var exception = new TlsHandshakeException("alert", inner);

        Assert.AreEqual("alert", exception.Message);
        Assert.AreSame(inner, exception.InnerException);
        Assert.IsInstanceOfType<IOException>(exception);
    }

    [TestMethod]
    public void TlsSession_KeepsEveryMember_AndComparesByValue()
    {
        var session = new TlsSession(SslProtocols.Tls13, TlsCipherSuite.TLS_AES_256_GCM_SHA384, "http/1.1", "localhost", null);

        Assert.AreEqual(SslProtocols.Tls13, session.Protocol);
        Assert.AreEqual(TlsCipherSuite.TLS_AES_256_GCM_SHA384, session.CipherSuite);
        Assert.AreEqual("http/1.1", session.ApplicationProtocol);
        Assert.AreEqual("localhost", session.ServerName);
        Assert.IsNull(session.ClientCertificate);
        Assert.AreEqual(session with { }, session);
    }
}
