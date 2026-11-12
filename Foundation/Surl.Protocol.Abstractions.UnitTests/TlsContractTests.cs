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
    [DataRow("s3", DisplayName = "The extended s3 is HTTPS (ADR-0108, decision 5)")]
    [DataRow("amqps", DisplayName = "The extended amqps is AMQP 0-9-1 over implicit TLS (ADR-0112, decision 8)")]
    [DataRow("mqtt5s", DisplayName = "The extended mqtt5s is MQTT 5.0 over implicit TLS (ADR-0093, decision 7)")]
    [DataRow("gs", DisplayName = "The extended gs is Google Cloud Storage over HTTPS (ADR-0110, decision 5)")]
    [DataRow("az", DisplayName = "The extended az is Azure Storage over HTTPS (ADR-0109, decision 6)")]
    [DataRow("abfss", DisplayName = "The extended abfss is Azure Data Lake over HTTPS (ADR-0109, decision 6)")]
    [DataRow("socketios", DisplayName = "The extended socketios is Socket.IO inside TLS (ADR-0120)")]
    [DataRow("xmpps", DisplayName = "The extended xmpps is XMPP with direct TLS (XEP-0368)")]
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
    [DataRow("abfs")]
    [DataRow("tftp")]
    [DataRow("socketio", DisplayName = "The extended socketio is plain HTTP (ADR-0120)")]
    [DataRow("oci", DisplayName = "An extended scheme no ADR serves over TLS")]
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
        Assert.IsNull(session.SrpUserName);
        Assert.AreEqual(session with { }, session);
    }

    [TestMethod]
    public void TlsSession_WithSrpUserName_KeepsItAndComparesByIt()
    {
        var session = new TlsSession(SslProtocols.Tls12, TlsCipherSuite.TLS_SRP_SHA_WITH_AES_256_CBC_SHA, null, null, null) { SrpUserName = "jsmith" };

        Assert.AreEqual("jsmith", session.SrpUserName);
        Assert.AreNotEqual(session with { SrpUserName = null }, session);
    }
}
