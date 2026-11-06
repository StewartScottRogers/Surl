namespace Surl.Tls13;

/// <summary>The server's key share and the shared secret of one key exchange.</summary>
/// <param name="ServerShare">The server's key_exchange value for its ServerHello.</param>
/// <param name="SharedSecret">The (EC)DHE shared secret, the key schedule's input.</param>
internal sealed record TlsKeyAgreement(byte[] ServerShare, byte[] SharedSecret);
