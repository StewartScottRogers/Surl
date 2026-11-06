using System.Text;

namespace Surl.Tls13;

/// <summary>
/// Builds the extensions a server sends (RFC 8446 section 4.2), each from typed values, ready for
/// a ServerHello, HelloRetryRequest, EncryptedExtensions or CertificateRequest.
/// </summary>
public static class TlsExtensions
{
    /// <summary>The TLS 1.3 version number, <c>0x0304</c>.</summary>
    public const ushort Tls13Version = 0x0304;

    /// <summary>supported_versions as a ServerHello or HelloRetryRequest carries it: the one selected version.</summary>
    /// <param name="version">The selected version; TLS 1.3 is <see cref="Tls13Version"/>.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension SupportedVersionsSelected(ushort version) =>
        new(TlsExtensionType.SupportedVersions, TlsWriter.Body(writer => writer.WriteUInt16(version)));

    /// <summary>key_share as a ServerHello carries it: the server's one share.</summary>
    /// <param name="share">The server's key share.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension KeyShareServer(TlsKeyShareEntry share)
    {
        ArgumentNullException.ThrowIfNull(share);
        return new(TlsExtensionType.KeyShare, TlsWriter.Body(writer =>
        {
            writer.WriteUInt16((ushort)share.Group);
            writer.WriteVector(2, share.KeyExchange.Span);
        }));
    }

    /// <summary>key_share as a HelloRetryRequest carries it: the group the client must send a share for.</summary>
    /// <param name="selectedGroup">The group the server selected.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension KeyShareHelloRetryRequest(TlsNamedGroup selectedGroup) =>
        new(TlsExtensionType.KeyShare, TlsWriter.Body(writer => writer.WriteUInt16((ushort)selectedGroup)));

    /// <summary>cookie (RFC 8446 section 4.2.2), which a HelloRetryRequest may carry.</summary>
    /// <param name="cookie">The cookie, one byte or more.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension Cookie(ReadOnlySpan<byte> cookie)
    {
        var value = cookie.ToArray();
        return new(TlsExtensionType.Cookie, TlsWriter.Body(writer => writer.WriteVector(2, value)));
    }

    /// <summary>supported_groups, which EncryptedExtensions may carry to tell the client the server's preference.</summary>
    /// <param name="groups">The groups, in the server's order of preference.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension SupportedGroups(IEnumerable<TlsNamedGroup> groups)
    {
        var values = groups.ToArray();
        return new(TlsExtensionType.SupportedGroups, TlsWriter.Body(writer => writer.WriteVector(2, list =>
        {
            foreach (var group in values)
            {
                list.WriteUInt16((ushort)group);
            }
        })));
    }

    /// <summary>signature_algorithms, which a CertificateRequest carries.</summary>
    /// <param name="schemes">The schemes, in the server's order of preference.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension SignatureAlgorithms(IEnumerable<TlsSignatureScheme> schemes)
    {
        var values = schemes.ToArray();
        return new(TlsExtensionType.SignatureAlgorithms, TlsWriter.Body(writer => writer.WriteVector(2, list =>
        {
            foreach (var scheme in values)
            {
                list.WriteUInt16((ushort)scheme);
            }
        })));
    }

    /// <summary>application_layer_protocol_negotiation as EncryptedExtensions carries it: the one selected protocol (RFC 7301 section 3.1).</summary>
    /// <param name="protocol">The selected protocol name, 1 to 255 bytes.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension ApplicationProtocolSelected(string protocol)
    {
        var name = Encoding.Latin1.GetBytes(protocol);
        return new(
            TlsExtensionType.ApplicationLayerProtocolNegotiation,
            TlsWriter.Body(writer => writer.WriteVector(2, list => list.WriteVector(1, name))));
    }

    /// <summary>server_name with empty data: EncryptedExtensions' acknowledgement that the server read the client's server_name (RFC 6066 section 3).</summary>
    /// <returns>The extension.</returns>
    public static TlsExtension ServerNameAcknowledged() => new(TlsExtensionType.ServerName, Array.Empty<byte>());

    /// <summary>record_size_limit (RFC 8449).</summary>
    /// <param name="limit">The largest record the sender is willing to receive.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension RecordSizeLimit(ushort limit) =>
        new(TlsExtensionType.RecordSizeLimit, TlsWriter.Body(writer => writer.WriteUInt16(limit)));

    /// <summary>quic_transport_parameters (RFC 9001 section 8.2): the transport parameters, as Surl.Quic encodes them.</summary>
    /// <param name="parameters">The encoded transport parameters.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension QuicTransportParameters(ReadOnlySpan<byte> parameters) =>
        new(TlsExtensionType.QuicTransportParameters, parameters.ToArray());
}
