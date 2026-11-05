namespace Surl.Dtls;

/// <summary>
/// What the server chose for one certificate handshake from the client's hello (ADR-0087
/// decisions 1, 2 and 5): the suite, the ECDHE group, the signature scheme, whether the extended
/// master secret is used, and the extensions the <c>ServerHello</c> answers with.
/// </summary>
/// <param name="Suite">The suite, the first in the server's order the client offers and the credential serves.</param>
/// <param name="Group">The ECDHE group.</param>
/// <param name="SignatureScheme">The scheme that signs <c>ServerKeyExchange</c>.</param>
/// <param name="UsesExtendedMasterSecret">Whether the client offered <c>extended_master_secret</c>.</param>
/// <param name="ServerExtensions">The <c>ServerHello</c>'s extensions.</param>
internal sealed record DtlsServerNegotiation(
    DtlsCipherSuite Suite,
    DtlsNamedGroup Group,
    DtlsSignatureScheme SignatureScheme,
    bool UsesExtendedMasterSecret,
    IReadOnlyList<DtlsExtension> ServerExtensions)
{
    /// <summary>The groups in the server's order: secp256r1, x25519, secp384r1.</summary>
    public static readonly IReadOnlyList<DtlsNamedGroup> GroupsInServerOrder =
        [DtlsNamedGroup.Secp256r1, DtlsNamedGroup.X25519, DtlsNamedGroup.Secp384r1];

    /// <summary><c>TLS_EMPTY_RENEGOTIATION_INFO_SCSV</c> (RFC 5746 section 3.3).</summary>
    public const ushort EmptyRenegotiationInfoScsv = 0x00FF;

    private const byte NullCompression = 0;
    private const byte UncompressedPointFormat = 0;

    /// <summary>
    /// Negotiates a certificate handshake.
    /// </summary>
    /// <param name="hello">The client's hello, its cookie already verified.</param>
    /// <param name="certificate">The server's certificate.</param>
    /// <param name="aesCcmSupported">Whether the platform has AES-CCM; without it no CCM suite is live.</param>
    /// <returns>What was chosen.</returns>
    /// <exception cref="DtlsAlertException">The alert ADR-0087 decision 10 names for what the hello lacks.</exception>
    /// <exception cref="DtlsDecodeErrorException">An extension the server reads is malformed.</exception>
    public static DtlsServerNegotiation Negotiate(DtlsClientHello hello, DtlsServerCertificate certificate, bool aesCcmSupported)
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

        var suite = ChooseSuite(hello, certificate, aesCcmSupported);
        var extensions = new List<DtlsExtension>();
        AnswerPointFormats(hello, extensions);
        var group = ChooseGroup(hello);
        var scheme = ChooseScheme(hello, certificate);
        var usesExtendedMasterSecret = Find(hello, DtlsExtensionType.ExtendedMasterSecret) is not null;
        if (usesExtendedMasterSecret)
        {
            extensions.Add(new DtlsExtension(DtlsExtensionType.ExtendedMasterSecret, []));
        }

        AnswerRenegotiationInfo(hello, extensions);
        return new DtlsServerNegotiation(suite, group, scheme, usesExtendedMasterSecret, extensions);
    }

    private static byte[]? Find(DtlsClientHello hello, DtlsExtensionType type) =>
        hello.Extensions.FirstOrDefault(extension => extension.Type == type)?.Data;

    private static DtlsCipherSuite ChooseSuite(DtlsClientHello hello, DtlsServerCertificate certificate, bool aesCcmSupported) =>
        DtlsCipherSuite.CertificateSuitesInServerOrder.FirstOrDefault(suite =>
            suite.KeyAlgorithm == certificate.KeyAlgorithm
            && (aesCcmSupported || suite.Aead == DtlsAeadAlgorithm.AesGcm)
            && hello.CipherSuites.Contains(suite.Code))
        ?? throw new DtlsAlertException(DtlsAlertDescription.HandshakeFailure, "The client offers no suite the server's certificate serves.");

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
