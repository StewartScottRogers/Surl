namespace Surl.Tls13;

/// <summary>
/// What one <see cref="TlsServerHandshake.Receive"/> call produced: the server's handshake bytes
/// for each encryption level, to send in that level's <c>CRYPTO</c> frames, and the traffic secrets
/// that became known.
/// </summary>
/// <param name="Initial">ServerHello or HelloRetryRequest bytes, to send at the Initial level; empty when there are none.</param>
/// <param name="Handshake">EncryptedExtensions, Certificate, CertificateVerify and Finished, to send at the Handshake level; empty when there are none.</param>
/// <param name="HandshakeSecrets">The handshake traffic secrets, when this call derived them; otherwise null.</param>
/// <param name="ApplicationSecrets">The application traffic secrets, when this call derived them; otherwise null.</param>
public sealed record TlsHandshakeOutput(
    byte[] Initial,
    byte[] Handshake,
    TlsTrafficSecrets? HandshakeSecrets,
    TlsTrafficSecrets? ApplicationSecrets);
