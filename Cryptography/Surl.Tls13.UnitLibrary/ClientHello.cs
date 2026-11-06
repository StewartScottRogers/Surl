using System.Text;

namespace Surl.Tls13;

/// <summary>
/// A ClientHello (RFC 8446 section 4.1.2), read from the wire with the extensions a QUIC server
/// acts on decoded (ADR-0082 decision 3). Every extension, decoded or not, is also in
/// <see cref="Extensions"/> in the order the client sent it. A decoded extension the client did not
/// send is null.
/// </summary>
public sealed class ClientHello
{
    private const ushort Tls12Version = 0x0303;
    private const int RandomLength = 32;
    private const int MaximumSessionIdLength = 32;

    private ClientHello(
        byte[] random,
        byte[] legacySessionId,
        IReadOnlyList<TlsCipherSuite> cipherSuites,
        IReadOnlyList<TlsExtension> extensions)
    {
        Random = random;
        LegacySessionId = legacySessionId;
        CipherSuites = cipherSuites;
        Extensions = extensions;
    }

    /// <summary>The client's 32 random bytes.</summary>
    public byte[] Random { get; }

    /// <summary>legacy_session_id: zero to 32 bytes the server echoes in its ServerHello.</summary>
    public byte[] LegacySessionId { get; }

    /// <summary>The cipher suites offered, in the client's order of preference.</summary>
    public IReadOnlyList<TlsCipherSuite> CipherSuites { get; }

    /// <summary>Every extension, in the order the client sent them.</summary>
    public IReadOnlyList<TlsExtension> Extensions { get; }

    /// <summary>The host_name of server_name (RFC 6066 section 3), or null when the client sent none.</summary>
    public string? ServerName { get; private init; }

    /// <summary>supported_versions: the versions offered, in the client's order.</summary>
    public IReadOnlyList<ushort>? SupportedVersions { get; private init; }

    /// <summary>supported_groups: the key exchange groups offered, in the client's order.</summary>
    public IReadOnlyList<TlsNamedGroup>? SupportedGroups { get; private init; }

    /// <summary>signature_algorithms: the signature schemes offered, in the client's order.</summary>
    public IReadOnlyList<TlsSignatureScheme>? SignatureAlgorithms { get; private init; }

    /// <summary>The ALPN protocol names offered (RFC 7301), in the client's order.</summary>
    public IReadOnlyList<string>? ApplicationProtocols { get; private init; }

    /// <summary>psk_key_exchange_modes: the modes offered.</summary>
    public IReadOnlyList<byte>? PskKeyExchangeModes { get; private init; }

    /// <summary>key_share: the client's key shares, in its order.</summary>
    public IReadOnlyList<TlsKeyShareEntry>? KeyShares { get; private init; }

    /// <summary>quic_transport_parameters (RFC 9001 section 8.2) as opaque bytes, which Surl.Quic parses.</summary>
    public byte[]? QuicTransportParameters { get; private init; }

    /// <summary>
    /// Reads a ClientHello handshake message: its one-byte type, three-byte length and body.
    /// </summary>
    /// <param name="message">The whole handshake message.</param>
    /// <returns>The ClientHello.</returns>
    /// <exception cref="TlsAlertException">
    /// The message is not a ClientHello (<see cref="TlsAlert.UnexpectedMessage"/>); a length
    /// overruns or a vector is out of its range (<see cref="TlsAlert.DecodeError"/>); the
    /// legacy_version is not <c>0x0303</c> (<see cref="TlsAlert.ProtocolVersion"/>); or the
    /// compression methods are not just null, an extension repeats, a group has two key shares, or
    /// pre_shared_key is not last (<see cref="TlsAlert.IllegalParameter"/>).
    /// </exception>
    public static ClientHello Read(ReadOnlySpan<byte> message)
    {
        var reader = new TlsReader(message);
        var type = reader.ReadUInt8();
        if (type != (byte)TlsHandshakeType.ClientHello)
        {
            throw new TlsAlertException(TlsAlert.UnexpectedMessage, $"Handshake message type {type} is not a ClientHello.");
        }

        var body = new TlsReader(reader.ReadBytes(reader.ReadUInt24()));
        reader.ExpectEnd("the ClientHello message");
        return ReadBody(ref body);
    }

    private static ClientHello ReadBody(ref TlsReader body)
    {
        var legacyVersion = body.ReadUInt16();
        if (legacyVersion != Tls12Version)
        {
            throw new TlsAlertException(
                TlsAlert.ProtocolVersion,
                $"legacy_version 0x{legacyVersion:X4} is not 0x0303, which every TLS 1.3 ClientHello sends.");
        }

        var random = body.ReadBytes(RandomLength).ToArray();
        var sessionId = ReadSessionId(ref body);
        var cipherSuites = Array.ConvertAll(
            TlsReader.ReadUInt16List(body.ReadVector16(0), "cipher suite"),
            value => (TlsCipherSuite)value);
        CheckCompressionMethods(body.ReadVector8(1));
        var extensions = body.IsAtEnd ? [] : TlsExtensionBlock.Read(body.ReadVector16(0));
        body.ExpectEnd("the ClientHello");
        CheckPreSharedKeyIsLast(extensions);
        return Decode(random, sessionId, cipherSuites, extensions);
    }

    private static ClientHello Decode(
        byte[] random, byte[] sessionId, TlsCipherSuite[] cipherSuites, List<TlsExtension> extensions)
    {
        return new ClientHello(random, sessionId, cipherSuites, extensions)
        {
            ServerName = ReadServerName(TlsExtensionBlock.Find(extensions, TlsExtensionType.ServerName)),
            SupportedVersions = ReadSupportedVersions(TlsExtensionBlock.Find(extensions, TlsExtensionType.SupportedVersions)),
            SupportedGroups = ConvertList(
                ReadUInt16ListExtension(TlsExtensionBlock.Find(extensions, TlsExtensionType.SupportedGroups), "supported_groups"),
                value => (TlsNamedGroup)value),
            SignatureAlgorithms = ConvertList(
                ReadUInt16ListExtension(TlsExtensionBlock.Find(extensions, TlsExtensionType.SignatureAlgorithms), "signature_algorithms"),
                value => (TlsSignatureScheme)value),
            ApplicationProtocols = ReadApplicationProtocols(
                TlsExtensionBlock.Find(extensions, TlsExtensionType.ApplicationLayerProtocolNegotiation)),
            PskKeyExchangeModes = ReadPskKeyExchangeModes(TlsExtensionBlock.Find(extensions, TlsExtensionType.PskKeyExchangeModes)),
            KeyShares = ReadKeyShares(TlsExtensionBlock.Find(extensions, TlsExtensionType.KeyShare)),
            QuicTransportParameters = TlsExtensionBlock.Find(extensions, TlsExtensionType.QuicTransportParameters)?.Data.ToArray(),
        };
    }

    private static byte[] ReadSessionId(ref TlsReader body)
    {
        var sessionId = body.ReadVector8(0);
        if (sessionId.Length > MaximumSessionIdLength)
        {
            throw new TlsAlertException(
                TlsAlert.DecodeError,
                $"legacy_session_id is {sessionId.Length} bytes; it may be at most {MaximumSessionIdLength}.");
        }

        return sessionId.ToArray();
    }

    private static void CheckCompressionMethods(ReadOnlySpan<byte> methods)
    {
        if (methods.Length != 1 || methods[0] != 0)
        {
            throw new TlsAlertException(
                TlsAlert.IllegalParameter,
                "legacy_compression_methods must be exactly the null method in a TLS 1.3 ClientHello.");
        }
    }

    private static void CheckPreSharedKeyIsLast(List<TlsExtension> extensions)
    {
        var index = extensions.FindIndex(extension => extension.Type == TlsExtensionType.PreSharedKey);
        if (index >= 0 && index != extensions.Count - 1)
        {
            throw new TlsAlertException(
                TlsAlert.IllegalParameter,
                "pre_shared_key must be the last extension of a ClientHello (RFC 8446 section 4.2.11).");
        }
    }

    private static string? ReadServerName(TlsExtension? extension)
    {
        if (extension is null)
        {
            return null;
        }

        var reader = new TlsReader(extension.Data.Span);
        var list = new TlsReader(reader.ReadVector16(1));
        reader.ExpectEnd("server_name");
        string? hostName = null;
        while (!list.IsAtEnd)
        {
            var nameType = list.ReadUInt8();
            var name = list.ReadVector16(1);
            hostName ??= nameType == 0 ? Encoding.ASCII.GetString(name) : null;
        }

        return hostName;
    }

    private static ushort[]? ReadSupportedVersions(TlsExtension? extension)
    {
        if (extension is null)
        {
            return null;
        }

        var reader = new TlsReader(extension.Data.Span);
        var versions = TlsReader.ReadUInt16List(reader.ReadVector8(0), "supported_versions");
        reader.ExpectEnd("supported_versions");
        return versions;
    }

    private static ushort[]? ReadUInt16ListExtension(TlsExtension? extension, string what)
    {
        if (extension is null)
        {
            return null;
        }

        var reader = new TlsReader(extension.Data.Span);
        var values = TlsReader.ReadUInt16List(reader.ReadVector16(0), what);
        reader.ExpectEnd(what);
        return values;
    }

    private static T[]? ConvertList<T>(ushort[]? values, Converter<ushort, T> convert) =>
        values is null ? null : Array.ConvertAll(values, convert);

    private static string[]? ReadApplicationProtocols(TlsExtension? extension)
    {
        if (extension is null)
        {
            return null;
        }

        var reader = new TlsReader(extension.Data.Span);
        var list = new TlsReader(reader.ReadVector16(2));
        reader.ExpectEnd("application_layer_protocol_negotiation");
        var names = new List<string>();
        while (!list.IsAtEnd)
        {
            names.Add(Encoding.Latin1.GetString(list.ReadVector8(1)));
        }

        return [.. names];
    }

    private static byte[]? ReadPskKeyExchangeModes(TlsExtension? extension)
    {
        if (extension is null)
        {
            return null;
        }

        var reader = new TlsReader(extension.Data.Span);
        var modes = reader.ReadVector8(1).ToArray();
        reader.ExpectEnd("psk_key_exchange_modes");
        return modes;
    }

    private static TlsKeyShareEntry[]? ReadKeyShares(TlsExtension? extension)
    {
        if (extension is null)
        {
            return null;
        }

        var reader = new TlsReader(extension.Data.Span);
        var list = new TlsReader(reader.ReadVector16(0));
        reader.ExpectEnd("key_share");
        var shares = new List<TlsKeyShareEntry>();
        while (!list.IsAtEnd)
        {
            var group = (TlsNamedGroup)list.ReadUInt16();
            var keyExchange = list.ReadVector16(1).ToArray();
            if (shares.Exists(share => share.Group == group))
            {
                throw new TlsAlertException(
                    TlsAlert.IllegalParameter,
                    $"key_share offers group 0x{(ushort)group:X4} twice (RFC 8446 section 4.2.8).");
            }

            shares.Add(new TlsKeyShareEntry(group, keyExchange));
        }

        return [.. shares];
    }
}
