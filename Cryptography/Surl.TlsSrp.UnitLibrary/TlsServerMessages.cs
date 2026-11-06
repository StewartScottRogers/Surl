using Surl.Cryptography.Srp;

namespace Surl.TlsSrp;

/// <summary>
/// Writes the handshake messages the server sends (RFC 5246 section 7.4, RFC 5054 section 2.8.2),
/// each framed with its four-byte header: type and 24-bit length.
/// </summary>
public static class TlsServerMessages
{
    private const byte NullCompression = 0;

    /// <summary>Frames <paramref name="body"/> as a handshake message of <paramref name="type"/>.</summary>
    /// <param name="type">The message type.</param>
    /// <param name="body">The message body.</param>
    /// <returns>The type, the 24-bit length and the body.</returns>
    public static byte[] Frame(TlsHandshakeType type, ReadOnlySpan<byte> body) =>
        [(byte)type, (byte)(body.Length >> 16), (byte)(body.Length >> 8), (byte)body.Length, .. body];

    /// <summary>
    /// The ServerHello: version <c>03 03</c>, the server random, an empty session ID, the suite,
    /// null compression, and the extensions <paramref name="negotiation"/> answers, in the order
    /// <c>renegotiation_info</c>, <c>extended_master_secret</c>, <c>encrypt_then_mac</c>, ALPN; with
    /// none to answer, the extensions block is left out (RFC 5246 section 7.4.1.3).
    /// </summary>
    /// <param name="negotiation">What was agreed.</param>
    /// <param name="serverRandom">The 32-byte server random.</param>
    /// <returns>The framed message.</returns>
    public static byte[] ServerHello(TlsSrpNegotiation negotiation, ReadOnlySpan<byte> serverRandom)
    {
        ArgumentNullException.ThrowIfNull(negotiation);
        var extensions = Extensions(negotiation);
        var suite = (ushort)negotiation.CipherSuite;
        byte[] body =
        [
            0x03, 0x03,
            .. serverRandom,
            0,
            (byte)(suite >> 8), (byte)suite,
            NullCompression,
            .. (extensions.Length == 0 ? [] : Vector16(extensions)),
        ];
        return Frame(TlsHandshakeType.ServerHello, body);
    }

    /// <summary>
    /// The ServerKeyExchange of an unsigned suite: <c>srp_N</c>, <c>srp_g</c>, <c>srp_s</c> and
    /// <c>srp_B</c>, each a vector of its unsigned big-endian bytes (RFC 5054 section 2.8.2).
    /// </summary>
    /// <param name="group">The account's group.</param>
    /// <param name="salt">The account's salt.</param>
    /// <param name="serverPublicValue">The server's <c>B</c>.</param>
    /// <returns>The framed message.</returns>
    public static byte[] ServerKeyExchange(SrpGroup group, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> serverPublicValue)
    {
        ArgumentNullException.ThrowIfNull(group);
        byte[] body =
        [
            .. Vector16(Srp6a.ToUnsignedBigEndian(group.Prime)),
            .. Vector16(Srp6a.ToUnsignedBigEndian(group.Generator)),
            (byte)salt.Length, .. salt,
            .. Vector16(serverPublicValue),
        ];
        return Frame(TlsHandshakeType.ServerKeyExchange, body);
    }

    /// <summary>The ServerHelloDone: an empty body.</summary>
    /// <returns>The framed message.</returns>
    public static byte[] ServerHelloDone() => Frame(TlsHandshakeType.ServerHelloDone, []);

    /// <summary>The Finished message carrying <paramref name="verifyData"/>.</summary>
    /// <param name="verifyData">The 12 bytes of <c>verify_data</c>.</param>
    /// <returns>The framed message.</returns>
    public static byte[] Finished(ReadOnlySpan<byte> verifyData) => Frame(TlsHandshakeType.Finished, verifyData);

    private static byte[] Extensions(TlsSrpNegotiation negotiation)
    {
        var extensions = new List<byte>();
        if (negotiation.AnswersRenegotiationInfo)
        {
            extensions.AddRange(Extension(TlsExtensionType.RenegotiationInfo, [0]));
        }

        if (negotiation.ExtendedMasterSecret)
        {
            extensions.AddRange(Extension(TlsExtensionType.ExtendedMasterSecret, []));
        }

        if (negotiation.EncryptThenMac)
        {
            extensions.AddRange(Extension(TlsExtensionType.EncryptThenMac, []));
        }

        if (negotiation.ApplicationProtocol is not null)
        {
            extensions.AddRange(Extension(TlsExtensionType.ApplicationLayerProtocolNegotiation, Vector16([8, .. "http/1.1"u8])));
        }

        return [.. extensions];
    }

    private static byte[] Extension(ushort type, ReadOnlySpan<byte> data) => [(byte)(type >> 8), (byte)type, .. Vector16(data)];

    private static byte[] Vector16(ReadOnlySpan<byte> data) => [(byte)(data.Length >> 8), (byte)data.Length, .. data];
}
