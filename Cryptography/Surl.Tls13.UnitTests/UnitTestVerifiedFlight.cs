using System.Security.Cryptography;

namespace Surl.Tls13;

/// <summary>
/// What <see cref="UnitTestTlsClient"/> derived from a server flight it verified. The client's
/// Finished covers the transcript through the server's Finished; after a CertificateRequest, the
/// client's Certificate and CertificateVerify come first, and
/// <see cref="UnitTestClientAuthentication"/> writes the Finished that covers them.
/// </summary>
internal sealed record UnitTestVerifiedFlight(
    ushort CipherSuite,
    byte[] EncryptedExtensions,
    byte[]? CertificateRequest,
    byte[] Certificate,
    byte[] CertificateVerify,
    TlsTrafficSecrets HandshakeSecrets,
    TlsTrafficSecrets ApplicationSecrets,
    byte[] ClientFinished,
    HashAlgorithmName Hash,
    byte[] TranscriptThroughServerFinished);
