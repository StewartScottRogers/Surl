namespace Surl.Tls13;

/// <summary>One KeyShareEntry of the key_share extension (RFC 8446 section 4.2.8).</summary>
/// <param name="Group">The group the key exchange value belongs to.</param>
/// <param name="KeyExchange">The key exchange value: a public key, or for X25519MLKEM768 an encapsulation key or ciphertext with an x25519 share.</param>
public sealed record TlsKeyShareEntry(TlsNamedGroup Group, ReadOnlyMemory<byte> KeyExchange);
