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
    public static byte[] ServerKeyExchange(SrpGroup group, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> serverPublicValue) =>
        Frame(TlsHandshakeType.ServerKeyExchange, ServerSrpParams(group, salt, serverPublicValue));

    /// <summary>
    /// The <c>ServerSRPParams</c> (RFC 5054 section 2.8.2): <c>srp_N</c>, <c>srp_g</c>, <c>srp_s</c>
    /// and <c>srp_B</c>. It is the whole body of an unsigned ServerKeyExchange and the part a signed one signs.
    /// </summary>
    /// <param name="group">The account's group.</param>
    /// <param name="salt">The account's salt.</param>
    /// <param name="serverPublicValue">The server's <c>B</c>.</param>
    /// <returns>The parameters, unframed.</returns>
    public static byte[] ServerSrpParams(SrpGroup group, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> serverPublicValue)
    {
        ArgumentNullException.ThrowIfNull(group);
        return
        [
            .. Vector16(Srp6a.ToUnsignedBigEndian(group.Prime)),
            .. Vector16(Srp6a.ToUnsignedBigEndian(group.Generator)),
            (byte)salt.Length, .. salt,
            .. Vector16(serverPublicValue),
        ];
    }

    /// <summary>
    /// The ServerKeyExchange of a signed suite: the parameters, then the TLS 1.2 <c>digitally-signed</c>
    /// struct (RFC 5246 section 7.4.3): the scheme and the signature as a vector.
    /// </summary>
    /// <param name="serverSrpParams">The parameters from <see cref="ServerSrpParams"/>.</param>
    /// <param name="scheme">The scheme the signature was made under.</param>
    /// <param name="signature">The signature.</param>
    /// <returns>The framed message.</returns>
    public static byte[] SignedServerKeyExchange(ReadOnlySpan<byte> serverSrpParams, TlsSignatureScheme scheme, ReadOnlySpan<byte> signature) =>
        Frame(TlsHandshakeType.ServerKeyExchange, [.. serverSrpParams, (byte)((ushort)scheme >> 8), (byte)scheme, .. Vector16(signature)]);

    /// <summary>The Certificate message: a 24-bit-length list of 24-bit-length DER certificates (RFC 5246 section 7.4.2).</summary>
    /// <param name="chain">The DER certificates, the server's own first.</param>
    /// <returns>The framed message.</returns>
    public static byte[] Certificate(IReadOnlyList<byte[]> chain)
    {
        ArgumentNullException.ThrowIfNull(chain);
        byte[] list = [.. chain.SelectMany(certificate => Vector24(certificate))];
        return Frame(TlsHandshakeType.Certificate, Vector24(list));
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

    private static byte[] Vector24(ReadOnlySpan<byte> data) => [(byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length, .. data];

    private static byte[] Vector16(ReadOnlySpan<byte> data) => [(byte)(data.Length >> 8), (byte)data.Length, .. data];
}
