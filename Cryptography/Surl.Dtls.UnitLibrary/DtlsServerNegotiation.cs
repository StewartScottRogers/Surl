namespace Surl.Dtls;

/// <summary>
/// What the server chose for one handshake from the client's hello (ADR-0087 decisions 1, 2 and
/// 5): the suite, for a certificate suite the ECDHE group and the signature scheme, whether the
/// extended master secret is used, and the extensions the <c>ServerHello</c> answers with.
/// </summary>
/// <param name="Suite">The suite, the first in the server's order the client offers and the credentials serve.</param>
/// <param name="Group">The ECDHE group, or <see langword="null"/> for a PSK suite.</param>
/// <param name="SignatureScheme">The scheme that signs <c>ServerKeyExchange</c>, or <see langword="null"/> for a PSK suite.</param>
/// <param name="UsesExtendedMasterSecret">Whether the client offered <c>extended_master_secret</c>.</param>
/// <param name="ServerExtensions">The <c>ServerHello</c>'s extensions.</param>
/// <param name="UsesRawPublicKey">
/// Whether the server's <c>Certificate</c> carries its key's <c>SubjectPublicKeyInfo</c> alone (RFC 7250).
/// </param>
/// <param name="MaximumFragmentLength">
/// The record plaintext limit the client asked for with <c>max_fragment_length</c> (RFC 6066
/// section 4), or <see langword="null"/> when it asked for none.
/// </param>
internal sealed record DtlsServerNegotiation(
    DtlsCipherSuite Suite,
    DtlsNamedGroup? Group,
    DtlsSignatureScheme? SignatureScheme,
    bool UsesExtendedMasterSecret,
    IReadOnlyList<DtlsExtension> ServerExtensions,
    bool UsesRawPublicKey,
    int? MaximumFragmentLength)
{
    /// <summary><c>X509</c> (0), a certificate type of RFC 7250 section 3.</summary>
    public const byte X509CertificateType = 0;

    /// <summary><c>RawPublicKey</c> (2), a certificate type of RFC 7250 section 3.</summary>
    public const byte RawPublicKeyCertificateType = 2;

    /// <summary>The groups in the server's order: secp256r1, x25519, secp384r1.</summary>
    public static readonly IReadOnlyList<DtlsNamedGroup> GroupsInServerOrder =
        [DtlsNamedGroup.Secp256r1, DtlsNamedGroup.X25519, DtlsNamedGroup.Secp384r1];

    /// <summary><c>TLS_EMPTY_RENEGOTIATION_INFO_SCSV</c> (RFC 5746 section 3.3).</summary>
    public const ushort EmptyRenegotiationInfoScsv = 0x00FF;

    private const byte NullCompression = 0;
    private const byte UncompressedPointFormat = 0;

    /// <summary>
    /// Negotiates a handshake.
    /// </summary>
    /// <param name="hello">The client's hello, its cookie already verified.</param>
    /// <param name="certificate">The server's certificate, or <see langword="null"/> when it has none.</param>
    /// <param name="preSharedKeysExist">Whether a PSK lookup exists, which makes the PSK suites live.</param>
    /// <param name="aesCcmSupported">Whether the platform has AES-CCM; without it no CCM suite is live.</param>
    /// <param name="requestsClientCertificate">
    /// Whether a certificate handshake asks for a client certificate (<c>--cacert</c>), which makes
    /// <c>client_certificate_type</c> answered (ADR-0087 decision 3).
    /// </param>
    /// <returns>What was chosen.</returns>
    /// <exception cref="DtlsAlertException">The alert ADR-0087 decision 10 names for what the hello lacks.</exception>
    /// <exception cref="DtlsDecodeErrorException">An extension the server reads is malformed.</exception>
    public static DtlsServerNegotiation Negotiate(
        DtlsClientHello hello,
        DtlsServerCertificate? certificate,
        bool preSharedKeysExist,
        bool aesCcmSupported,
        bool requestsClientCertificate = false)
    {
        if (hello.ClientVersion > DtlsRecordHeader.Dtls12Version)
        {
            throw new DtlsAlertException(
                DtlsAlertDescription.ProtocolVersion, $"client_version 0x{hello.ClientVersion:X4} is below DTLS 1.2.");
        }

        if (!hello.CompressionMethods.Contains(NullCompression))
        {
            throw new DtlsAlertException(DtlsAlertDescription.IllegalParameter, "The hello does not offer null compression.");
        }

        var suite = ChooseSuite(hello, certificate, preSharedKeysExist, aesCcmSupported);
        var extensions = new List<DtlsExtension>();
        DtlsNamedGroup? group = null;
        DtlsSignatureScheme? scheme = null;
        var usesRawPublicKey = false;
        if (!suite.UsesPreSharedKey)
        {
            AnswerPointFormats(hello, extensions);
            group = ChooseGroup(hello);
            scheme = ChooseScheme(hello, certificate!);
            usesRawPublicKey = AnswerServerCertificateType(hello, extensions);
            AnswerClientCertificateType(hello, extensions, requestsClientCertificate);
        }

        var usesExtendedMasterSecret = Find(hello, DtlsExtensionType.ExtendedMasterSecret) is not null;
        if (usesExtendedMasterSecret)
        {
            extensions.Add(new DtlsExtension(DtlsExtensionType.ExtendedMasterSecret, []));
        }

        AnswerRenegotiationInfo(hello, extensions);
        var maximumFragmentLength = AnswerMaxFragmentLength(hello, extensions);
        return new DtlsServerNegotiation(
            suite, group, scheme, usesExtendedMasterSecret, extensions, usesRawPublicKey, maximumFragmentLength);
    }

    /// <summary>
    /// Answers <c>server_certificate_type</c> with the first type in the client's list the server
    /// can send, <c>X509</c> or <c>RawPublicKey</c> (RFC 7250 section 4.2, ADR-0087 decision 3).
    /// </summary>
    /// <returns>Whether the server sends its raw public key.</returns>
    private static bool AnswerServerCertificateType(DtlsClientHello hello, List<DtlsExtension> extensions)
    {
        if (Find(hello, DtlsExtensionType.ServerCertificateType) is not { } data)
        {
            return false;
        }

        var type = FirstCertificateTypeServed(data, servesRawPublicKey: true);
        extensions.Add(new DtlsExtension(DtlsExtensionType.ServerCertificateType, [type]));
        return type == RawPublicKeyCertificateType;
    }

    /// <summary>
    /// Answers <c>client_certificate_type</c> with <c>X509</c> when a client certificate is
    /// requested; client raw public keys are not accepted, as nothing names the keys to trust
    /// (ADR-0087 decision 3). Without a request the extension is ignored.
    /// </summary>
    private static void AnswerClientCertificateType(DtlsClientHello hello, List<DtlsExtension> extensions, bool requestsClientCertificate)
    {
        if (requestsClientCertificate && Find(hello, DtlsExtensionType.ClientCertificateType) is { } data)
        {
            extensions.Add(new DtlsExtension(
                DtlsExtensionType.ClientCertificateType, [FirstCertificateTypeServed(data, servesRawPublicKey: false)]));
        }
    }

    private static byte FirstCertificateTypeServed(byte[] data, bool servesRawPublicKey)
    {
        var reader = new DtlsHandshakeBodyReader(data);
        var types = reader.ReadVector8(1, byte.MaxValue);
        reader.ExpectEnd();
        foreach (var type in types)
        {
            if (type == X509CertificateType || (servesRawPublicKey && type == RawPublicKeyCertificateType))
            {
                return type;
            }
        }

        throw new DtlsAlertException(DtlsAlertDescription.UnsupportedCertificate, "The client lists no certificate type the server serves.");
    }

    /// <summary>
    /// Honours and echoes <c>max_fragment_length</c> (RFC 6066 section 4): codes 1 to 4 ask for
    /// 2^9 to 2^12 plaintext bytes per record; any other code is <c>illegal_parameter</c>.
    /// </summary>
    /// <returns>The plaintext limit, or <see langword="null"/> when the client asked for none.</returns>
    private static int? AnswerMaxFragmentLength(DtlsClientHello hello, List<DtlsExtension> extensions)
    {
        if (Find(hello, DtlsExtensionType.MaxFragmentLength) is not { } data)
        {
            return null;
        }

        if (data is not [var code])
        {
            throw new DtlsDecodeErrorException("max_fragment_length carries one byte.");
        }

        if (code is < 1 or > 4)
        {
            throw new DtlsAlertException(DtlsAlertDescription.IllegalParameter, $"max_fragment_length code {code} is not 1 to 4.");
        }

        extensions.Add(new DtlsExtension(DtlsExtensionType.MaxFragmentLength, [code]));
        return 1 << (8 + code);
    }

    private static byte[]? Find(DtlsClientHello hello, DtlsExtensionType type) =>
        hello.Extensions.FirstOrDefault(extension => extension.Type == type)?.Data;

    private static DtlsCipherSuite ChooseSuite(
        DtlsClientHello hello, DtlsServerCertificate? certificate, bool preSharedKeysExist, bool aesCcmSupported) =>
        DtlsCipherSuite.AllSuitesInServerOrder.FirstOrDefault(suite =>
            (suite.UsesPreSharedKey ? preSharedKeysExist : suite.KeyAlgorithm == certificate?.KeyAlgorithm)
            && (aesCcmSupported || suite.Aead == DtlsAeadAlgorithm.AesGcm)
            && hello.CipherSuites.Contains(suite.Code))
        ?? throw new DtlsAlertException(DtlsAlertDescription.HandshakeFailure, "The client offers no suite the server's credentials serve.");

    private static void AnswerPointFormats(DtlsClientHello hello, List<DtlsExtension> extensions)
    {
        if (Find(hello, DtlsExtensionType.EcPointFormats) is not { } data)
        {
            return;
        }

        var reader = new DtlsHandshakeBodyReader(data);
        var formats = reader.ReadVector8(1, byte.MaxValue);
        reader.ExpectEnd();
        if (!formats.Contains(UncompressedPointFormat))
        {
            throw new DtlsAlertException(DtlsAlertDescription.IllegalParameter, "The client does not accept uncompressed points.");
        }

        extensions.Add(new DtlsExtension(DtlsExtensionType.EcPointFormats, [1, UncompressedPointFormat]));
    }

    private static DtlsNamedGroup ChooseGroup(DtlsClientHello hello)
    {
        if (Find(hello, DtlsExtensionType.SupportedGroups) is not { } data)
        {
            return DtlsNamedGroup.Secp256r1;
        }

        var offered = ReadUInt16List(data);
        foreach (var group in GroupsInServerOrder)
        {
            if (offered.Contains((ushort)group))
            {
                return group;
            }
        }

        throw new DtlsAlertException(DtlsAlertDescription.HandshakeFailure, "The client offers no group the server exchanges keys on.");
    }

    private static DtlsSignatureScheme ChooseScheme(DtlsClientHello hello, DtlsServerCertificate certificate)
    {
        if (Find(hello, DtlsExtensionType.SignatureAlgorithms) is not { } data)
        {
            throw new DtlsAlertException(
                DtlsAlertDescription.HandshakeFailure, "The client sent no signature_algorithms, which would mean SHA-1.");
        }

        return certificate.ChooseScheme(ReadUInt16List(data))
            ?? throw new DtlsAlertException(
                DtlsAlertDescription.HandshakeFailure, "The client offers no signature scheme the server's key can make.");
    }

    private static void AnswerRenegotiationInfo(DtlsClientHello hello, List<DtlsExtension> extensions)
    {
        var data = Find(hello, DtlsExtensionType.RenegotiationInfo);
        if (data is not (null or [0]))
        {
            throw new DtlsAlertException(
                DtlsAlertDescription.HandshakeFailure, "An initial handshake's renegotiation_info must be empty (RFC 5746 section 3.6).");
        }

        if (data is not null || hello.CipherSuites.Contains(EmptyRenegotiationInfoScsv))
        {
            extensions.Add(new DtlsExtension(DtlsExtensionType.RenegotiationInfo, [0]));
        }
    }

    private static ushort[] ReadUInt16List(byte[] data)
    {
        var reader = new DtlsHandshakeBodyReader(data);
        var list = reader.ReadUInt16Vector16(2, ushort.MaxValue - 1);
        reader.ExpectEnd();
        return list;
    }
}
