using System.Text;

namespace Surl.TlsSrp;

/// <summary>
/// What the server agrees from a ClientHello (ADR-0122 decisions 2 and 3): the suite, the SRP user
/// name, and which extensions it answers.
/// </summary>
/// <param name="CipherSuite">The first suite in the client's order the server can serve.</param>
/// <param name="UserName">The <c>srp</c> extension's user name, as sent.</param>
/// <param name="ExtendedMasterSecret">Whether RFC 7627's extended master secret is used.</param>
/// <param name="EncryptThenMac">Whether RFC 7366's encrypt-then-MAC is used.</param>
/// <param name="AnswersRenegotiationInfo">Whether the ServerHello carries an empty <c>renegotiation_info</c>.</param>
/// <param name="ApplicationProtocol"><c>http/1.1</c> when ALPN was offered, otherwise <see langword="null"/>.</param>
/// <param name="ServerName">The SNI host name, or <see langword="null"/> when none was sent.</param>
public sealed record TlsSrpNegotiation(
    TlsSrpCipherSuite CipherSuite,
    byte[] UserName,
    bool ExtendedMasterSecret,
    bool EncryptThenMac,
    bool AnswersRenegotiationInfo,
    string? ApplicationProtocol,
    string? ServerName)
{
    /// <summary>The one application protocol the server agrees: HTTP/2 is forbidden over CBC suites (RFC 9113 section 9.2.2).</summary>
    public const string Http11 = "http/1.1";

    /// <summary>
    /// The suites the server can serve without a certificate: RFC 5054's three unsigned ones
    /// (ADR-0122 decision 2).
    /// </summary>
    public static IReadOnlyList<TlsSrpCipherSuite> UnsignedSuites { get; } =
        [TlsSrpCipherSuite.SrpSha3DesEdeCbcSha, TlsSrpCipherSuite.SrpShaAes128CbcSha, TlsSrpCipherSuite.SrpShaAes256CbcSha];

    private const ushort Tls12 = 0x0303;
    private const ushort EmptyRenegotiationSignalingSuite = 0x00FF;
    private const byte NullCompression = 0;
    private const byte HostNameType = 0;

    /// <summary>Agrees the handshake's parameters from <paramref name="hello"/>.</summary>
    /// <param name="hello">The client's hello.</param>
    /// <returns>What was agreed.</returns>
    /// <exception cref="TlsAlertException">The alert ADR-0122 decision 6 names for the hello's fault.</exception>
    public static TlsSrpNegotiation Negotiate(TlsClientHello hello)
    {
        ArgumentNullException.ThrowIfNull(hello);
        RequireTls12(hello);
        Require(hello.CompressionMethods.Contains(NullCompression), TlsAlertDescription.IllegalParameter, "The ClientHello does not offer null compression.");
        var suite = ChooseSuite(hello.CipherSuites);
        var extensions = hello.Extensions;
        return new TlsSrpNegotiation(
            suite,
            ReadUserName(extensions),
            extensions.ContainsKey(TlsExtensionType.ExtendedMasterSecret),
            extensions.ContainsKey(TlsExtensionType.EncryptThenMac),
            AnswersRenegotiation(hello),
            ChooseApplicationProtocol(extensions),
            ReadServerName(extensions));
    }

    private static void RequireTls12(TlsClientHello hello)
    {
        if (hello.ClientVersion >= Tls12)
        {
            return;
        }

        var offersTls12 = hello.Extensions.TryGetValue(TlsExtensionType.SupportedVersions, out var data)
            && ReadSupportedVersions(data).Contains(Tls12);
        Require(offersTls12, TlsAlertDescription.ProtocolVersion, $"The ClientHello's version {hello.ClientVersion:X4} is below TLS 1.2.");
    }

    private static List<ushort> ReadSupportedVersions(byte[] data)
    {
        var reader = new TlsMessageReader(data, "supported_versions");
        var list = new TlsMessageReader(reader.ReadVector8(), "the supported_versions list");
        reader.ExpectEnd();
        var versions = new List<ushort>();
        while (!list.IsAtEnd)
        {
            versions.Add(list.ReadUInt16());
        }

        return versions;
    }

    private static TlsSrpCipherSuite ChooseSuite(IReadOnlyList<ushort> offered)
    {
        foreach (var code in offered)
        {
            if (UnsignedSuites.Contains((TlsSrpCipherSuite)code))
            {
                return (TlsSrpCipherSuite)code;
            }
        }

        throw new TlsAlertException(TlsAlertDescription.HandshakeFailure, "The ClientHello offers no SRP suite the server can serve.");
    }

    private static byte[] ReadUserName(IReadOnlyDictionary<ushort, byte[]> extensions)
    {
        if (!extensions.TryGetValue(TlsExtensionType.Srp, out var data))
        {
            throw new TlsAlertException(TlsAlertDescription.UnknownPskIdentity, "The ClientHello offers only SRP suites but has no srp extension.");
        }

        var reader = new TlsMessageReader(data, "the srp extension");
        var userName = reader.ReadVector8();
        reader.ExpectEnd();
        Require(userName.Length > 0, TlsAlertDescription.DecodeError, "The srp extension's user name is empty.");
        return userName;
    }

    private static bool AnswersRenegotiation(TlsClientHello hello)
    {
        if (hello.Extensions.TryGetValue(TlsExtensionType.RenegotiationInfo, out var data))
        {
            Require(data is [0], TlsAlertDescription.HandshakeFailure, "An initial ClientHello's renegotiation_info is not empty.");
            return true;
        }

        return hello.CipherSuites.Contains(EmptyRenegotiationSignalingSuite);
    }

    private static string? ChooseApplicationProtocol(IReadOnlyDictionary<ushort, byte[]> extensions)
    {
        if (!extensions.TryGetValue(TlsExtensionType.ApplicationLayerProtocolNegotiation, out var data))
        {
            return null;
        }

        var reader = new TlsMessageReader(data, "the ALPN extension");
        var list = new TlsMessageReader(reader.ReadVector16(), "the ALPN list");
        reader.ExpectEnd();
        var offersHttp11 = false;
        while (!list.IsAtEnd)
        {
            var name = list.ReadVector8();
            Require(name.Length > 0, TlsAlertDescription.DecodeError, "The ALPN list holds an empty protocol name.");
            offersHttp11 |= name.AsSpan().SequenceEqual("http/1.1"u8);
        }

        Require(offersHttp11, TlsAlertDescription.NoApplicationProtocol, "The ALPN list does not offer http/1.1.");
        return Http11;
    }

    private static string? ReadServerName(IReadOnlyDictionary<ushort, byte[]> extensions)
    {
        if (!extensions.TryGetValue(TlsExtensionType.ServerName, out var data))
        {
            return null;
        }

        var reader = new TlsMessageReader(data, "the server_name extension");
        var list = new TlsMessageReader(reader.ReadVector16(), "the server_name list");
        reader.ExpectEnd();
        string? hostName = null;
        while (!list.IsAtEnd)
        {
            var type = list.ReadByte();
            var name = list.ReadVector16();
            hostName ??= type == HostNameType ? Encoding.ASCII.GetString(name) : null;
        }

        return hostName;
    }

    private static void Require(bool condition, TlsAlertDescription description, string message)
    {
        if (!condition)
        {
            throw new TlsAlertException(description, message);
        }
    }
}
