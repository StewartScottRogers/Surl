namespace Surl.Protocol.Ssh;

/// <summary>
/// The algorithms the server's <c>SSH_MSG_KEXINIT</c> offers, each list in the server's order
/// of preference, the same in both directions (ADR-0051, decision 2).
/// </summary>
/// <param name="KeyExchange">The key exchange methods, ending with the strict key exchange marker.</param>
/// <param name="ServerHostKey">The host-key algorithms: only those of the host keys the server holds.</param>
/// <param name="Cipher">The ciphers.</param>
/// <param name="Mac">The MACs.</param>
/// <param name="Compression">The compression methods.</param>
public sealed record SshAlgorithmOffer(
    IReadOnlyList<string> KeyExchange,
    IReadOnlyList<string> ServerHostKey,
    IReadOnlyList<string> Cipher,
    IReadOnlyList<string> Mac,
    IReadOnlyList<string> Compression)
{
    /// <summary>
    /// The pseudo-algorithm the server lists last among its key exchange methods: it will
    /// hold a client that lists <see cref="StrictKeyExchangeClientMarker"/> to strict key
    /// exchange (OpenSSH <c>PROTOCOL</c>, ADR-0051 decision 2.1).
    /// </summary>
    public const string StrictKeyExchangeServerMarker = "kex-strict-s-v00@openssh.com";

    /// <summary>
    /// The pseudo-algorithm a client lists to ask for strict key exchange.
    /// </summary>
    public const string StrictKeyExchangeClientMarker = "kex-strict-c-v00@openssh.com";

    /// <summary>
    /// The pseudo-algorithm a client lists to take <c>SSH_MSG_EXT_INFO</c> after the first
    /// <c>NEWKEYS</c> (RFC 8308, section 2.1; ADR-0051 decision 2.1).
    /// </summary>
    public const string ExtensionInfoClientMarker = "ext-info-c";

    private static readonly string[] HostKeyOrder =
    [
        "ssh-ed25519",
        "ecdsa-sha2-nistp256",
        "ecdsa-sha2-nistp384",
        "ecdsa-sha2-nistp521",
        "rsa-sha2-512",
        "rsa-sha2-256",
    ];

    private static readonly string[] WeakKeyExchange = ["diffie-hellman-group14-sha1", "diffie-hellman-group-exchange-sha1", "diffie-hellman-group1-sha1"];

    private static readonly string[] WeakHostKeyOrder = [SshRsaHostKey.RsaKeyType, SshDsaHostKey.DsaKeyType];

    private static readonly string[] WeakCipher = ["aes256-cbc", "rijndael-cbc@lysator.liu.se", "aes192-cbc", "aes128-cbc", "3des-cbc", "arcfour128", "arcfour", "blowfish-cbc", "cast128-cbc"];

    private static readonly string[] WeakMac = ["hmac-sha1-etm@openssh.com", "hmac-sha1", "hmac-sha1-96", "hmac-md5", "hmac-md5-96", "hmac-ripemd160", "hmac-ripemd160@openssh.com"];

    /// <summary>
    /// Whether the offer is <c>--allow-weak-ssh-algorithms</c>'s (ADR-0051, decision 5): the
    /// weak algorithms are listed, and <c>ssh-rsa</c> and <c>ssh-dss</c> user-key signatures and
    /// RSA user keys shorter than 2048 bits are accepted at login.
    /// </summary>
    public bool AllowsWeakAlgorithms { get; init; }

    /// <summary>
    /// The default offer of ADR-0051 decision 2: every default key exchange method, cipher, MAC
    /// and compression method in the decision's order, and of its host-key algorithms those
    /// in <paramref name="heldHostKeyAlgorithms"/>, still in the decision's order, each
    /// certificate algorithm (<c>&lt;name&gt;-cert-v01@openssh.com</c>) just before its key's. With
    /// <paramref name="allowWeakAlgorithms"/>, each list also offers the decision's weak
    /// algorithms after its default ones - the key exchange methods before the strict key
    /// exchange marker, which stays last.
    /// </summary>
    /// <param name="heldHostKeyAlgorithms">
    /// The host-key algorithms the server's keys sign with, e.g. <c>rsa-sha2-512</c> and
    /// <c>rsa-sha2-256</c> for an RSA key. A name the decision does not list is left out.
    /// </param>
    /// <param name="aesGcmIsSupported">
    /// Whether <c>aes256-gcm@openssh.com</c> and <c>aes128-gcm@openssh.com</c> are offered:
    /// <see cref="System.Security.Cryptography.AesGcm.IsSupported"/> on the machine serving.
    /// </param>
    /// <param name="allowWeakAlgorithms">Whether <c>--allow-weak-ssh-algorithms</c> was given.</param>
    /// <returns>The offer.</returns>
    public static SshAlgorithmOffer Default(IEnumerable<string> heldHostKeyAlgorithms, bool aesGcmIsSupported, bool allowWeakAlgorithms = false)
    {
        ArgumentNullException.ThrowIfNull(heldHostKeyAlgorithms);

        var held = heldHostKeyAlgorithms.ToHashSet(StringComparer.Ordinal);
        string[] aesGcm = aesGcmIsSupported ? ["aes256-gcm@openssh.com", "aes128-gcm@openssh.com"] : [];
        string[] Weak(string[] names) => allowWeakAlgorithms ? names : [];

        return new SshAlgorithmOffer(
            [
                "curve25519-sha256",
                "curve25519-sha256@libssh.org",
                "ecdh-sha2-nistp256",
                "ecdh-sha2-nistp384",
                "ecdh-sha2-nistp521",
                "diffie-hellman-group-exchange-sha256",
                "diffie-hellman-group16-sha512",
                "diffie-hellman-group18-sha512",
                "diffie-hellman-group14-sha256",
                .. Weak(WeakKeyExchange),
                StrictKeyExchangeServerMarker,
            ],
            [.. WithCertificateNames(HostKeyOrder.Concat(Weak(WeakHostKeyOrder))).Where(held.Contains)],
            ["chacha20-poly1305@openssh.com", .. aesGcm, "aes256-ctr", "aes192-ctr", "aes128-ctr", .. Weak(WeakCipher)],
            ["hmac-sha2-256-etm@openssh.com", "hmac-sha2-512-etm@openssh.com", "hmac-sha2-256", "hmac-sha2-512", .. Weak(WeakMac)],
            ["none", "zlib@openssh.com", "zlib"])
        {
            AllowsWeakAlgorithms = allowWeakAlgorithms,
        };
    }

    /// <summary>
    /// This offer with its cipher and MAC lists narrowed to the names given, in the order given,
    /// as sshd's <c>Ciphers</c> and <c>MACs</c> narrow its own (<c>--ssh-ciphers</c> and
    /// <c>--ssh-macs</c>, ADR-0066). A name this offer does not list is left out, and a name
    /// given twice counts once; a list not given stays as it is.
    /// </summary>
    /// <param name="ciphers">The ciphers to offer, or <see langword="null"/> to keep <see cref="Cipher"/>.</param>
    /// <param name="macs">The MACs to offer, or <see langword="null"/> to keep <see cref="Mac"/>.</param>
    /// <returns>The narrowed offer.</returns>
    public SshAlgorithmOffer Narrowed(IReadOnlyList<string>? ciphers, IReadOnlyList<string>? macs) =>
        this with
        {
            Cipher = Narrow(Cipher, ciphers),
            Mac = Narrow(Mac, macs),
        };

    // Each host-key algorithm preceded by its certificate algorithm, which a --hostcert
    // certificate signs with (ADR-0051, decision 2).
    private static IEnumerable<string> WithCertificateNames(IEnumerable<string> hostKeyAlgorithms) =>
        hostKeyAlgorithms.SelectMany(algorithm => new[] { algorithm + SshHostCertificate.CertificateSuffix, algorithm });

    private static IReadOnlyList<string> Narrow(IReadOnlyList<string> offered, IReadOnlyList<string>? given) =>
        given is null ? offered : [.. given.Distinct(StringComparer.Ordinal).Where(offered.Contains)];
}
