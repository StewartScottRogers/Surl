namespace Surl.Tls13;

/// <summary>What <see cref="UnitTestTlsClient"/> derived from a server flight it verified.</summary>
internal sealed record UnitTestVerifiedFlight(
    ushort CipherSuite,
    byte[] EncryptedExtensions,
    byte[] Certificate,
    byte[] CertificateVerify,
    TlsTrafficSecrets HandshakeSecrets,
    TlsTrafficSecrets ApplicationSecrets,
    byte[] ClientFinished);
