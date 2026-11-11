namespace Surl.Tls13;

/// <summary>
/// The parameters a server chooses from a ClientHello (RFC 8446 section 4.1.1) as ADR-0082
/// decision 3 decides: the cipher suite, the signature scheme and the ALPN protocol, and the
/// key exchange group.
/// </summary>
/// <param name="CipherSuite">The first suite in the client's order that the server supports.</param>
/// <param name="SignatureScheme">The scheme the certificate key signs with, which the client offered.</param>
/// <param name="ApplicationProtocol">The agreed ALPN protocol, or null when ALPN is not negotiated.</param>
internal sealed record TlsServerNegotiation(
    TlsCipherSuite CipherSuite, TlsSignatureScheme SignatureScheme, string? ApplicationProtocol)
{
    /// <summary>
    /// The key exchange groups the server serves, in its order of preference: ADR-0082 decision 3's
    /// five, then the groups a curl offers only when <c>--curves</c> names one alone (ADR-0147).
    /// </summary>
    public static IReadOnlyList<TlsNamedGroup> GroupPreference { get; } =
    [
        TlsNamedGroup.X25519MlKem768, TlsNamedGroup.X25519, TlsNamedGroup.Secp256r1, TlsNamedGroup.Secp384r1, TlsNamedGroup.Secp521r1,
        TlsNamedGroup.Ffdhe2048, TlsNamedGroup.Ffdhe3072,
    ];

    private static readonly TlsCipherSuite[] SupportedCipherSuites =
        [TlsCipherSuite.Aes128GcmSha256, TlsCipherSuite.Aes256GcmSha384, TlsCipherSuite.ChaCha20Poly1305Sha256];

    /// <summary>Chooses the parameters for <paramref name="hello"/>, or throws the alert that ends the handshake.</summary>
    /// <exception cref="TlsAlertException">
    /// TLS 1.3 is not offered (<see cref="TlsAlert.ProtocolVersion"/>); supported_groups,
    /// signature_algorithms or a required quic_transport_parameters is absent
    /// (<see cref="TlsAlert.MissingExtension"/>); no cipher suite or signature scheme is shared
    /// (<see cref="TlsAlert.HandshakeFailure"/>); or no ALPN protocol is shared
    /// (<see cref="TlsAlert.NoApplicationProtocol"/>).
    /// </exception>
    public static TlsServerNegotiation Choose(ClientHello hello, TlsServerHandshakeOptions options, TlsSignatureScheme keyScheme)
    {
        if (hello.SupportedVersions?.Contains(TlsExtensions.Tls13Version) != true)
        {
            throw new TlsAlertException(TlsAlert.ProtocolVersion, "The ClientHello does not offer TLS 1.3 in supported_versions.");
        }

        RequireExtensions(hello, options);
        return new TlsServerNegotiation(
            ChooseCipherSuite(hello),
            ChooseSignatureScheme(hello, keyScheme),
            ChooseApplicationProtocol(hello, options.ApplicationProtocols));
    }

    /// <summary>The first group in <see cref="GroupPreference"/> the client sent a key share for, or null when there is none.</summary>
    public static TlsKeyShareEntry? ChooseKeyShare(ClientHello hello) =>
        GroupPreference
            .Select(group => hello.KeyShares?.FirstOrDefault(share => share.Group == group))
            .FirstOrDefault(share => share is not null);

    /// <summary>The group a HelloRetryRequest names: the first in <see cref="GroupPreference"/> that the client's supported_groups lists.</summary>
    /// <exception cref="TlsAlertException">The client supports none of them (<see cref="TlsAlert.HandshakeFailure"/>).</exception>
    public static TlsNamedGroup ChooseRetryGroup(ClientHello hello)
    {
        foreach (var group in GroupPreference.Where(group => hello.SupportedGroups!.Contains(group)))
        {
            return group;
        }

        throw new TlsAlertException(TlsAlert.HandshakeFailure, "The client supports no key exchange group the server serves.");
    }

    private static void RequireExtensions(ClientHello hello, TlsServerHandshakeOptions options)
    {
        if (hello.SupportedGroups is null || hello.SignatureAlgorithms is null)
        {
            throw new TlsAlertException(
                TlsAlert.MissingExtension,
                "A ClientHello without a pre-shared key must send supported_groups and signature_algorithms (RFC 8446 section 9.2).");
        }

        if (options.QuicTransportParameters is not null && hello.QuicTransportParameters is null)
        {
            throw new TlsAlertException(
                TlsAlert.MissingExtension,
                "The ClientHello has no quic_transport_parameters (RFC 9001 section 8.2).");
        }
    }

    private static TlsCipherSuite ChooseCipherSuite(ClientHello hello)
    {
        foreach (var suite in hello.CipherSuites.Where(SupportedCipherSuites.Contains))
        {
            return suite;
        }

        throw new TlsAlertException(TlsAlert.HandshakeFailure, "The client offers no TLS 1.3 cipher suite the server supports.");
    }

    private static TlsSignatureScheme ChooseSignatureScheme(ClientHello hello, TlsSignatureScheme keyScheme)
    {
        if (!hello.SignatureAlgorithms!.Contains(keyScheme))
        {
            throw new TlsAlertException(
                TlsAlert.HandshakeFailure,
                $"The client does not offer signature scheme 0x{(ushort)keyScheme:X4}, which the certificate key signs with.");
        }

        return keyScheme;
    }

    private static string? ChooseApplicationProtocol(ClientHello hello, IReadOnlyList<string> serverProtocols)
    {
        if (serverProtocols.Count == 0)
        {
            return null;
        }

        var offered = hello.ApplicationProtocols ?? [];
        foreach (var protocol in serverProtocols.Where(offered.Contains))
        {
            return protocol;
        }

        throw new TlsAlertException(
            TlsAlert.NoApplicationProtocol,
            $"The client offers no ALPN protocol the server agrees ({string.Join(", ", serverProtocols)}).");
    }
}
