namespace Surl.Tls13;

/// <summary>
/// A ServerHello (RFC 8446 section 4.1.3), or a HelloRetryRequest, which is a ServerHello whose
/// random is <see cref="HelloRetryRequestRandom"/> (section 4.1.4).
/// </summary>
/// <param name="Random">The server's 32 random bytes.</param>
/// <param name="LegacySessionIdEcho">The ClientHello's legacy_session_id, echoed.</param>
/// <param name="CipherSuite">The selected cipher suite.</param>
/// <param name="Extensions">The extensions, in the order they are written.</param>
public sealed record ServerHello(
    ReadOnlyMemory<byte> Random,
    ReadOnlyMemory<byte> LegacySessionIdEcho,
    TlsCipherSuite CipherSuite,
    IReadOnlyList<TlsExtension> Extensions)
{
    private const int RandomLength = 32;

    /// <summary>The random every HelloRetryRequest carries: SHA-256 of "HelloRetryRequest" (RFC 8446 section 4.1.3).</summary>
    public static ReadOnlyMemory<byte> HelloRetryRequestRandom { get; } = new byte[]
    {
        0xCF, 0x21, 0xAD, 0x74, 0xE5, 0x9A, 0x61, 0x11, 0xBE, 0x1D, 0x8C, 0x02, 0x1E, 0x65, 0xB8, 0x91,
        0xC2, 0xA2, 0x11, 0x16, 0x7A, 0xBB, 0x8C, 0x5E, 0x07, 0x9E, 0x09, 0xE2, 0xC8, 0xA8, 0x33, 0x9C,
    };

    /// <summary>A HelloRetryRequest: a ServerHello with <see cref="HelloRetryRequestRandom"/>.</summary>
    /// <param name="legacySessionIdEcho">The ClientHello's legacy_session_id, echoed.</param>
    /// <param name="cipherSuite">The selected cipher suite.</param>
    /// <param name="extensions">The extensions: key_share naming the selected group, supported_versions and any cookie.</param>
    /// <returns>The HelloRetryRequest.</returns>
    public static ServerHello HelloRetryRequest(
        ReadOnlyMemory<byte> legacySessionIdEcho, TlsCipherSuite cipherSuite, IReadOnlyList<TlsExtension> extensions) =>
        new(HelloRetryRequestRandom, legacySessionIdEcho, cipherSuite, extensions);

    /// <summary>Writes the handshake message: type, length and body.</summary>
    /// <returns>The message bytes.</returns>
    public byte[] Write()
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(Random.Length, RandomLength, nameof(Random));
        return TlsWriter.Handshake(TlsHandshakeType.ServerHello, writer =>
        {
            writer.WriteUInt16(0x0303);
            writer.WriteBytes(Random.Span);
            writer.WriteVector(1, LegacySessionIdEcho.Span);
            writer.WriteUInt16((ushort)CipherSuite);
            writer.WriteUInt8(0);
            writer.WriteExtensions(Extensions);
        });
    }
}
