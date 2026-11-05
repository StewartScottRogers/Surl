namespace Surl.Tls13;

/// <summary>A pair of traffic secrets for one encryption level (RFC 8446 section 7.1): the client's and the server's.</summary>
/// <param name="Client">The client's traffic secret: what the server decrypts with.</param>
/// <param name="Server">The server's traffic secret: what the server encrypts with.</param>
public sealed record TlsTrafficSecrets(byte[] Client, byte[] Server);
