using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Surl.Tls13;

namespace Surl.Tls13Stream;

/// <summary>A server stream that has completed a handshake with <see cref="UnitTestClient"/> over an in-memory connection.</summary>
internal sealed class UnitTestEstablished : IAsyncDisposable
{
    private UnitTestEstablished(Tls13ServerStream stream, UnitTestDuplex serverEnd, UnitTestDuplex clientEnd, UnitTestClient client)
    {
        Stream = stream;
        ServerEnd = serverEnd;
        ClientEnd = clientEnd;
        Client = client;
    }

    public Tls13ServerStream Stream { get; }

    public UnitTestDuplex ServerEnd { get; }

    public UnitTestDuplex ClientEnd { get; }

    public UnitTestClient Client { get; }

    /// <summary>Options serving a fresh self-signed ECDSA P-256 certificate and offering ALPN <paramref name="protocols"/>.</summary>
    public static TlsServerHandshakeOptions Options(params string[] protocols)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256);
        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
        return new TlsServerHandshakeOptions
        {
            CertificateChain = [certificate.Export(X509ContentType.Cert)],
            CertificateKey = key,
            ApplicationProtocols = protocols,
        };
    }

    /// <summary>Options as <see cref="Options"/> gives, requiring a client certificate that <paramref name="trusts"/> accepts.</summary>
    public static TlsServerHandshakeOptions OptionsRequiringClientCertificate(
        Func<IReadOnlyList<ReadOnlyMemory<byte>>, bool> trusts)
    {
        var options = Options();
        return new TlsServerHandshakeOptions
        {
            CertificateChain = options.CertificateChain,
            CertificateKey = options.CertificateKey,
            TrustsClientCertificateChain = trusts,
        };
    }

    /// <summary>A server random and x25519 private key scripted from RFC 8448 section 3.</summary>
    public static UnitTestScriptedRandom Rfc8448Random() =>
        new(UnitTestRfc8448.ServerRandom, UnitTestRfc8448.ServerEphemeralPrivateKey);

    public static async Task<UnitTestEstablished> CreateAsync(byte[]? clientHello = null, params string[] protocols)
    {
        var (serverEnd, clientEnd) = UnitTestDuplex.CreatePair();
        var client = new UnitTestClient(clientEnd);
        var accepting = Tls13ServerStream.AcceptAsync(serverEnd, Options(protocols), RandomNumberGenerator.Create());
        await client.HandshakeAsync(clientHello ?? UnitTestRfc8448.ClientHello);
        return new UnitTestEstablished(await accepting, serverEnd, clientEnd, client);
    }

    public async ValueTask DisposeAsync()
    {
        await Stream.DisposeAsync();
        ClientEnd.Dispose();
    }
}
