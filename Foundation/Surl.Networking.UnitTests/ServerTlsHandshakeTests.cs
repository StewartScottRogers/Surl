using System.Net.Security;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// Which handshake a listener gets. The handshake itself runs in
/// <see cref="StreamConnectionTlsTests"/>, through the connection that owns it.
/// </summary>
[TestClass]
public sealed class ServerTlsHandshakeTests
{
    [TestMethod]
    public void ForListener_NoSettings_IsNull() =>
        Assert.IsNull(ServerTlsHandshake.ForListener(null, new ListenUrl("https", "127.0.0.1", 443)));

    [TestMethod]
    public void ForListener_Settings_OffersTheSchemesProtocols()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], new FixedTimeProvider(TestCertificates.Now));

        var handshake = ServerTlsHandshake.ForListener(settings, new ListenUrl("https", "127.0.0.1", 443))!;

        CollectionAssert.AreEqual(new[] { SslApplicationProtocol.Http11 }, handshake.ApplicationProtocols.ToArray());
    }
}
