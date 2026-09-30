using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Reads a <c>--hostkey</c> file's bytes as ADR-0051 decision 4 says: one PEM block holding
/// one private key, recognised by its label - <c>OPENSSH PRIVATE KEY</c> (<c>openssh-key-v1</c>),
/// <c>PRIVATE KEY</c> (PKCS #8), <c>ENCRYPTED PRIVATE KEY</c> (PKCS #8 under PBES2),
/// <c>RSA PRIVATE KEY</c> (PKCS #1) or <c>EC PRIVATE KEY</c> (SEC 1). Reading the file from
/// disk is <c>Surl.Console</c>'s (BL-171).
/// </summary>
/// <remarks>
/// RSA keys of at least 2048 bits, ECDSA keys on P-256, P-384 and P-521, and Ed25519 keys (in
/// PKCS #8, encrypted or not, and <c>openssh-key-v1</c>) are served. A shorter RSA key is
/// served only with <c>--allow-weak-ssh-algorithms</c>, and so is a DSA key (PKCS #8, encrypted or
/// not, and <c>openssh-key-v1</c>), as <c>ssh-dss</c> when its q is 160 bits. A PEM block
/// with RFC 1421 headers (the legacy <c>Proc-Type: 4,ENCRYPTED</c> form) is not a block this
/// reads, so it is not a private key surl can read.
/// </remarks>
public static class SshHostKeyFile
{
    /// <summary>The fewest bits an RSA host key has without <c>--allow-weak-ssh-algorithms</c>.</summary>
    public const int MinRsaBits = 2048;

    private const string RsaEncryptionOid = "1.2.840.113549.1.1.1";

    private const string EcPublicKeyOid = "1.2.840.10045.2.1";

    private const string DsaOid = "1.2.840.10040.4.1";

    private const string Ed25519Oid = "1.3.101.112";

    private static readonly Asn1Tag CurveParametersTag = new(TagClass.ContextSpecific, 0);

    // Each PEM label read, with the decoder of its body: the DER bytes, --pass and
    // --allow-weak-ssh-algorithms.
    private static readonly Dictionary<string, Func<byte[], string?, bool, SshHostKey>> DecodersByLabel = new(StringComparer.Ordinal)
    {
        ["OPENSSH PRIVATE KEY"] = (der, passphrase, allowWeak) => SshOpenSshKeyDecoder.Decode(der, passphrase, allowWeak),
        ["PRIVATE KEY"] = (der, _, allowWeak) => DecodePkcs8(der, allowWeak),
        ["ENCRYPTED PRIVATE KEY"] = (der, passphrase, allowWeak) => DecodePkcs8(SshPkcs8Decryption.Decrypt(der, passphrase), allowWeak),
        ["RSA PRIVATE KEY"] = (der, _, allowWeak) => DecodePkcs1(der, allowWeak),
        ["EC PRIVATE KEY"] = (der, _, _) => DecodeSec1(der),
    };

    /// <summary>
    /// Reads one host key from a file's bytes.
    /// </summary>
    /// <param name="fileBytes">The file's bytes.</param>
    /// <param name="passphrase">The <c>--pass</c> value, which decrypts an encrypted PKCS #8 or <c>openssh-key-v1</c> key; <see langword="null"/> when not given.</param>
    /// <param name="allowWeakAlgorithms">Whether <c>--allow-weak-ssh-algorithms</c> was given.</param>
    /// <returns>The key, or the refusal.</returns>
    public static SshHostKeyReading Read(ReadOnlySpan<byte> fileBytes, string? passphrase, bool allowWeakAlgorithms)
    {
        try
        {
            return new SshHostKeyReading(Decode(Encoding.Latin1.GetString(fileBytes), passphrase, allowWeakAlgorithms), null);
        }
        catch (SshHostKeyRefusedException refused)
        {
            return new SshHostKeyReading(null, refused.Refusal);
        }
        catch (Exception malformed) when (malformed is CryptographicException or AsnContentException or SshDisconnectRequiredException)
        {
            return new SshHostKeyReading(null, SshHostKeyRefusal.NotAPrivateKey);
        }
    }

    /// <summary>
    /// The host key for an RSA private key: refused as weak under <see cref="MinRsaBits"/>
    /// bits unless weak algorithms are allowed.
    /// </summary>
    /// <param name="rsa">The key.</param>
    /// <param name="allowWeakAlgorithms">Whether weak algorithms are allowed.</param>
    /// <returns>The host key.</returns>
    /// <exception cref="SshHostKeyRefusedException">The key is too short.</exception>
    internal static SshHostKey FromRsa(RSA rsa, bool allowWeakAlgorithms)
    {
        var parameters = rsa.ExportParameters(includePrivateParameters: true);
        var bits = SshRsaHostKey.ModulusBits(parameters);

        return bits < MinRsaBits && !allowWeakAlgorithms
            ? throw new SshHostKeyRefusedException(SshHostKeyRefusal.NeedsWeakAlgorithms("RSA", bits))
            : new SshRsaHostKey(parameters);
    }

    /// <summary>
    /// Refuses a DSA key as weak unless <c>--allow-weak-ssh-algorithms</c> was given; read before
    /// the rest of the key, so a DSA key without it is refused for its size alone.
    /// </summary>
    /// <param name="prime">The key's prime p.</param>
    /// <param name="allowWeakAlgorithms">Whether weak algorithms are allowed.</param>
    /// <exception cref="SshHostKeyRefusedException">Weak algorithms are not allowed.</exception>
    internal static void RequireWeakAlgorithmsForDsa(BigInteger prime, bool allowWeakAlgorithms)
    {
        if (!allowWeakAlgorithms)
        {
            throw new SshHostKeyRefusedException(SshHostKeyRefusal.NeedsWeakAlgorithms("DSA", prime.GetBitLength()));
        }
    }

    /// <summary>
    /// The <c>ssh-dss</c> host key for a DSA private key's values, checked so a damaged key is
    /// refused rather than served with signatures that do not verify.
    /// </summary>
    /// <param name="prime">p.</param>
    /// <param name="subgroupOrder">q, which must be 160 bits, the only size an <c>ssh-dss</c> signature holds.</param>
    /// <param name="generator">g.</param>
    /// <param name="publicValue">y as the file holds it, which must be g^x mod p; <see langword="null"/> when the file holds none (PKCS #8).</param>
    /// <param name="privateValue">x.</param>
    /// <returns>The host key.</returns>
    /// <exception cref="SshHostKeyRefusedException">q is not 160 bits.</exception>
    /// <exception cref="CryptographicException">The values do not form a DSA key.</exception>
    internal static SshHostKey FromDsa(BigInteger prime, BigInteger subgroupOrder, BigInteger generator, BigInteger? publicValue, BigInteger privateValue)
    {
        RequireDsaValuesInRange(prime, subgroupOrder, generator, privateValue);
        if (subgroupOrder.GetBitLength() != SshDsaHostKey.SubgroupLength * 8)
        {
            throw new SshHostKeyRefusedException(SshHostKeyRefusal.UnsupportedKeyType(SshDsaHostKey.DsaKeyType));
        }

        if (!BigInteger.ModPow(generator, subgroupOrder, prime).IsOne)
        {
            throw new CryptographicException("The DSA key's g does not generate a subgroup of order q.");
        }

        var y = BigInteger.ModPow(generator, privateValue, prime);
        if (publicValue is { } given && given != y)
        {
            throw new CryptographicException("The DSA key's public value is not its private value's.");
        }

        var primeLength = prime.GetByteCount(isUnsigned: true);
        var parameters = new DSAParameters
        {
            P = FixedLength(prime, primeLength),
            Q = FixedLength(subgroupOrder, SshDsaHostKey.SubgroupLength),
            G = FixedLength(generator, primeLength),
            Y = FixedLength(y, primeLength),
            X = FixedLength(privateValue, SshDsaHostKey.SubgroupLength),
        };

        // The platform's DSA refuses sizes it cannot sign with (Windows' CNG, a p over 1024 bits
        // with a 160-bit q) here, while reading the file, rather than at the first handshake.
        using (DSA.Create(parameters))
        {
            return new SshDsaHostKey(parameters);
        }
    }

    /// <summary>
    /// The curve a key file names by object identifier.
    /// </summary>
    /// <param name="oid">The curve's object identifier.</param>
    /// <returns>The curve.</returns>
    /// <exception cref="SshHostKeyRefusedException">It is none of the three NIST curves.</exception>
    internal static SshNistCurve CurveForOid(string oid) =>
        SshNistCurve.All.FirstOrDefault(curve => curve.Oid == oid)
            ?? throw new SshHostKeyRefusedException(SshHostKeyRefusal.UnsupportedKeyType("ecdsa on curve " + oid));

    private static SshHostKey Decode(string text, string? passphrase, bool allowWeakAlgorithms)
    {
        if (!PemEncoding.TryFind(text, out var fields) || PemEncoding.TryFind(text.AsSpan(fields.Location.End.Value), out _))
        {
            throw new SshHostKeyRefusedException(SshHostKeyRefusal.NotAPrivateKey);
        }

        var der = Convert.FromBase64String(text[fields.Base64Data]);

        return DecodersByLabel.TryGetValue(text[fields.Label], out var decode)
            ? decode(der, passphrase, allowWeakAlgorithms)
            : throw new SshHostKeyRefusedException(SshHostKeyRefusal.NotAPrivateKey);
    }

    private static SshHostKey DecodePkcs1(byte[] der, bool allowWeakAlgorithms)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(der, out _);

        return FromRsa(rsa, allowWeakAlgorithms);
    }

    // PrivateKeyInfo (RFC 5958): version, AlgorithmIdentifier, privateKey.
    private static SshHostKey DecodePkcs8(byte[] der, bool allowWeakAlgorithms)
    {
        var info = new AsnReader(der, AsnEncodingRules.DER).ReadSequence();
        info.ReadInteger();
        var algorithm = info.ReadSequence();
        var algorithmOid = algorithm.ReadObjectIdentifier();

        return algorithmOid switch
        {
            RsaEncryptionOid => DecodePkcs8Rsa(der, allowWeakAlgorithms),
            EcPublicKeyOid => DecodePkcs8Ecdsa(der, CurveForOid(algorithm.ReadObjectIdentifier())),
            DsaOid => DecodePkcs8Dsa(algorithm, info, allowWeakAlgorithms),
            Ed25519Oid => DecodePkcs8Ed25519(algorithm, info),
            _ => throw new SshHostKeyRefusedException(SshHostKeyRefusal.UnsupportedKeyType(algorithmOid)),
        };
    }

    private static SshHostKey DecodePkcs8Rsa(byte[] der, bool allowWeakAlgorithms)
    {
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(der, out _);

        return FromRsa(rsa, allowWeakAlgorithms);
    }

    // RFC 3279 section 2.3.2: the algorithm's parameters are Dss-Parms (p, q, g), and the
    // private key octets hold x as an INTEGER. y is computed.
    private static SshHostKey DecodePkcs8Dsa(AsnReader algorithm, AsnReader info, bool allowWeakAlgorithms)
    {
        var domain = algorithm.ReadSequence();
        var prime = domain.ReadInteger();
        RequireWeakAlgorithmsForDsa(prime, allowWeakAlgorithms);
        var subgroupOrder = domain.ReadInteger();
        var generator = domain.ReadInteger();
        var privateValue = new AsnReader(info.ReadOctetString(), AsnEncodingRules.DER).ReadInteger();

        return FromDsa(prime, subgroupOrder, generator, null, privateValue);
    }

    // 1 < q < p, 1 < g < p and 0 < x < q, checked before any exponentiation.
    private static void RequireDsaValuesInRange(BigInteger prime, BigInteger subgroupOrder, BigInteger generator, BigInteger privateValue)
    {
        if (!IsBetween(subgroupOrder, BigInteger.One, prime) || !IsBetween(generator, BigInteger.One, prime) || !IsBetween(privateValue, BigInteger.Zero, subgroupOrder))
        {
            throw new CryptographicException("The DSA key's values are out of range.");
        }
    }

    // Whether low < value < high.
    private static bool IsBetween(BigInteger value, BigInteger low, BigInteger high) => value > low && value < high;

    private static byte[] FixedLength(BigInteger value, int length)
    {
        var bytes = new byte[length];
        value.TryWriteBytes(bytes.AsSpan(length - value.GetByteCount(isUnsigned: true)), out _, isUnsigned: true, isBigEndian: true);

        return bytes;
    }

    private static SshHostKey DecodePkcs8Ecdsa(byte[] der, SshNistCurve curve)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(der, out _);

        return new SshEcdsaHostKey(curve, ecdsa.ExportParameters(includePrivateParameters: true));
    }

    // RFC 8410 section 7: the algorithm has no parameters, and the private key octets hold
    // CurvePrivateKey, an OCTET STRING of the 32-byte seed. The attributes and public key a
    // version 2 OneAsymmetricKey may carry after it are not read: the key served is the seed's.
    private static SshHostKey DecodePkcs8Ed25519(AsnReader algorithm, AsnReader info)
    {
        algorithm.ThrowIfNotEmpty();
        var curvePrivateKey = new AsnReader(info.ReadOctetString(), AsnEncodingRules.DER);
        var seed = curvePrivateKey.ReadOctetString();
        curvePrivateKey.ThrowIfNotEmpty();

        return SshEd25519HostKey.FromSeed(seed);
    }

    // ECPrivateKey (RFC 5915): version, privateKey, then the curve [0] this reader needs.
    private static SshHostKey DecodeSec1(byte[] der)
    {
        var key = new AsnReader(der, AsnEncodingRules.DER).ReadSequence();
        key.ReadInteger();
        key.ReadOctetString();
        var curve = CurveForOid(key.ReadSequence(CurveParametersTag).ReadObjectIdentifier());
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportECPrivateKey(der, out _);

        return new SshEcdsaHostKey(curve, ecdsa.ExportParameters(includePrivateParameters: true));
    }
}
