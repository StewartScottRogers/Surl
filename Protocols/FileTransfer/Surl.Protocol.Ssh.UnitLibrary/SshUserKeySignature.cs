using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Surl.Cryptography.Ed25519;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Verifies the signature a <c>publickey</c> login carries (RFC 4252, section 7) with the user's
/// public key and the algorithm the request names: <c>ssh-ed25519</c> (RFC 8709, section 6) with
/// the hand-built <see cref="Ed25519"/>, and <c>ecdsa-sha2-nistp256</c>, <c>-nistp384</c> and
/// <c>-nistp521</c> (RFC 5656, section 3.1.2) and <c>rsa-sha2-512</c> and <c>rsa-sha2-256</c>
/// (RFC 8332, section 3) with the base class library (ADR-0051, decision 6). With
/// <c>--allow-weak-ssh-algorithms</c> it also verifies <c>ssh-rsa</c> (PKCS #1 v1.5 over SHA-1)
/// and <c>ssh-dss</c> (DSA over SHA-1, r and s in 40 bytes; RFC 4253, section 6.6), and accepts
/// RSA keys shorter than <see cref="SshHostKeyFile.MinRsaBits"/> bits, which it otherwise refuses.
/// </summary>
internal static class SshUserKeySignature
{
    private const string RsaSha512 = "rsa-sha2-512";

    /// <summary>
    /// The user-key signature algorithms verified without <c>--allow-weak-ssh-algorithms</c>, in
    /// ADR-0051 decision 2's host-key order: what <c>EXT_INFO</c>'s <c>server-sig-algs</c> lists
    /// (RFC 8308, section 3.1).
    /// </summary>
    public static IReadOnlyList<string> Algorithms { get; } =
        Array.AsReadOnly([SshEd25519HostKey.Ed25519KeyType, "ecdsa-sha2-nistp256", "ecdsa-sha2-nistp384", "ecdsa-sha2-nistp521", RsaSha512, "rsa-sha2-256"]);

    /// <summary>
    /// <see cref="Algorithms"/> followed by <c>ssh-rsa</c> and <c>ssh-dss</c>: what is verified,
    /// and listed in <c>server-sig-algs</c>, with <c>--allow-weak-ssh-algorithms</c>.
    /// </summary>
    public static IReadOnlyList<string> AlgorithmsWithWeak { get; } =
        Array.AsReadOnly([.. Algorithms, SshRsaHostKey.RsaKeyType, SshDsaHostKey.DsaKeyType]);

    /// <summary>
    /// The algorithms verified: <see cref="AlgorithmsWithWeak"/> or <see cref="Algorithms"/>.
    /// </summary>
    /// <param name="allowWeakAlgorithms">Whether <c>--allow-weak-ssh-algorithms</c> was given.</param>
    /// <returns>The algorithms, in <c>server-sig-algs</c>'s order.</returns>
    public static IReadOnlyList<string> AlgorithmsFor(bool allowWeakAlgorithms) => allowWeakAlgorithms ? AlgorithmsWithWeak : Algorithms;

    /// <summary>
    /// The key type a key signing with <paramref name="algorithm"/> has: <c>ssh-rsa</c> for
    /// <c>rsa-sha2-*</c>, the algorithm's own name for the others.
    /// </summary>
    /// <param name="algorithm">The signature algorithm a request names.</param>
    /// <param name="allowWeakAlgorithms">Whether <c>ssh-rsa</c> and <c>ssh-dss</c> are verified.</param>
    /// <returns>The key type, or <see langword="null"/> when the algorithm is not verified.</returns>
    public static string? KeyTypeFor(string algorithm, bool allowWeakAlgorithms) =>
        !AlgorithmsFor(allowWeakAlgorithms).Contains(algorithm) ? null
        : algorithm.StartsWith("rsa-", StringComparison.Ordinal) ? SshRsaHostKey.RsaKeyType
        : algorithm;

    /// <summary>
    /// Whether a login may use <paramref name="keyBlob"/> with <paramref name="algorithm"/>: the
    /// algorithm is verified, the blob is of <see cref="KeyTypeFor"/> its type, and an RSA key has
    /// at least <see cref="SshHostKeyFile.MinRsaBits"/> bits unless weak algorithms are allowed.
    /// A blob too short to hold its key is refused.
    /// </summary>
    /// <param name="algorithm">The signature algorithm the request names.</param>
    /// <param name="keyBlob">The public key blob (RFC 4253, section 6.6).</param>
    /// <param name="allowWeakAlgorithms">Whether <c>--allow-weak-ssh-algorithms</c> was given.</param>
    /// <returns>Whether the key is accepted for the algorithm.</returns>
    public static bool AcceptsKey(string algorithm, ReadOnlyMemory<byte> keyBlob, bool allowWeakAlgorithms)
    {
        try
        {
            var key = new SshWireReader(keyBlob);
            var keyType = Encoding.Latin1.GetString(key.ReadString().Span);

            return KeyTypeFor(algorithm, allowWeakAlgorithms) == keyType
                && (allowWeakAlgorithms || keyType != SshRsaHostKey.RsaKeyType || RsaModulusBits(key) >= SshHostKeyFile.MinRsaBits);
        }
        catch (SshDisconnectRequiredException)
        {
            return false;
        }
    }

    /// <summary>
    /// Whether <paramref name="signature"/> is <paramref name="algorithm"/>'s signature over
    /// <paramref name="signedData"/> by the key in <paramref name="keyBlob"/>. A blob or
    /// signature that is malformed, names another algorithm or holds a value out of range does
    /// not verify.
    /// </summary>
    /// <param name="algorithm">One of <see cref="AlgorithmsWithWeak"/>, which <see cref="AcceptsKey"/> accepted for the blob.</param>
    /// <param name="keyBlob">The public key blob (RFC 4253, section 6.6).</param>
    /// <param name="signature">The signature field: <c>string</c> the algorithm, <c>string</c> the signature.</param>
    /// <param name="signedData">What the client signed.</param>
    /// <returns>Whether the signature verifies.</returns>
    public static bool Verifies(string algorithm, ReadOnlyMemory<byte> keyBlob, ReadOnlyMemory<byte> signature, byte[] signedData)
    {
        try
        {
            var signatureReader = new SshWireReader(signature);
            if (Encoding.Latin1.GetString(signatureReader.ReadString().Span) != algorithm)
            {
                return false;
            }

            var rawSignature = signatureReader.ReadString();
            var key = new SshWireReader(keyBlob);
            key.ReadString();

            return algorithm switch
            {
                SshEd25519HostKey.Ed25519KeyType => VerifiesEd25519(key, rawSignature, signedData),
                SshDsaHostKey.DsaKeyType => VerifiesDsa(key, rawSignature, signedData),
                _ when algorithm.StartsWith("ecdsa-", StringComparison.Ordinal) => VerifiesEcdsa(algorithm, key, rawSignature, signedData),
                _ => VerifiesRsa(algorithm, key, rawSignature, signedData),
            };
        }
        catch (Exception exception) when (exception is SshDisconnectRequiredException or CryptographicException)
        {
            return false;
        }
    }

    // The blob after its type: mpint e, mpint n.
    private static long RsaModulusBits(SshWireReader key)
    {
        key.ReadMpint();

        return key.ReadMpint().GetBitLength();
    }

    // The blob after its type: string the 32-byte public key. The signature is the 64 bytes of
    // RFC 8032 (RFC 8709, section 6); a key or signature of another length does not verify.
    private static bool VerifiesEd25519(SshWireReader key, ReadOnlyMemory<byte> rawSignature, byte[] signedData)
    {
        var publicKey = key.ReadString();

        return publicKey.Length == Ed25519.PublicKeySize
            && rawSignature.Length == Ed25519.SignatureSize
            && Ed25519.Verify(publicKey.Span, signedData, rawSignature.Span);
    }

    // The blob after its type: mpint e, mpint n (RFC 4253, section 6.6). The signature is the
    // modulus's length, or shorter with its leading zero bytes left off, which OpenSSH accepts too.
    private static bool VerifiesRsa(string algorithm, SshWireReader key, ReadOnlyMemory<byte> rawSignature, byte[] signedData)
    {
        var exponent = key.ReadMpint();
        var modulus = key.ReadMpint();
        if (exponent.Sign <= 0 || modulus.Sign <= 0)
        {
            return false;
        }

        var modulusBytes = modulus.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (rawSignature.Length > modulusBytes.Length)
        {
            return false;
        }

        var padded = new byte[modulusBytes.Length];
        rawSignature.Span.CopyTo(padded.AsSpan(padded.Length - rawSignature.Length));
        using var rsa = RSA.Create(new RSAParameters
        {
            Exponent = exponent.ToByteArray(isUnsigned: true, isBigEndian: true),
            Modulus = modulusBytes,
        });

        return rsa.VerifyData(signedData, padded, SshRsaHostKey.HashFor(algorithm), RSASignaturePadding.Pkcs1);
    }

    // The blob after its type: mpint p, mpint q, mpint g, mpint y. The signature is r and s as
    // 160-bit unsigned integers, 40 bytes (RFC 4253, section 6.6); q must be 160 bits to hold
    // them, and g and y no longer than p.
    private static bool VerifiesDsa(SshWireReader key, ReadOnlyMemory<byte> rawSignature, byte[] signedData)
    {
        var prime = key.ReadMpint();
        var primeLength = (int)((prime.GetBitLength() + 7) / 8);
        var parameters = new DSAParameters
        {
            P = new byte[primeLength],
            Q = new byte[SshDsaHostKey.SubgroupLength],
            G = new byte[primeLength],
            Y = new byte[primeLength],
        };
        if (rawSignature.Length != 2 * SshDsaHostKey.SubgroupLength
            || !TryWriteField(prime, parameters.P)
            || !TryWriteField(key.ReadMpint(), parameters.Q)
            || !TryWriteField(key.ReadMpint(), parameters.G)
            || !TryWriteField(key.ReadMpint(), parameters.Y))
        {
            return false;
        }

        using var dsa = DSA.Create(parameters);

        return dsa.VerifyData(signedData, rawSignature.Span, HashAlgorithmName.SHA1, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    // The blob after its type: string the curve's name, string Q. The signature holds mpint r,
    // mpint s (RFC 5656, section 3.1.2), each positive and no longer than the field.
    private static bool VerifiesEcdsa(string algorithm, SshWireReader key, ReadOnlyMemory<byte> rawSignature, byte[] signedData)
    {
        var curve = SshNistCurve.All.First(candidate => SshEcdsaHostKey.KeyTypeOn(candidate) == algorithm);
        if (Encoding.Latin1.GetString(key.ReadString().Span) != curve.Identifier)
        {
            return false;
        }

        var parameters = curve.DecodePublicPoint(key.ReadString().Span);
        var values = new SshWireReader(rawSignature);
        var fixedFields = new byte[2 * curve.FieldLength];
        if (!TryWriteField(values.ReadMpint(), fixedFields.AsSpan(0, curve.FieldLength))
            || !TryWriteField(values.ReadMpint(), fixedFields.AsSpan(curve.FieldLength)))
        {
            return false;
        }

        using var ecdsa = ECDsa.Create(parameters);

        return ecdsa.VerifyData(signedData, fixedFields, curve.HashAlgorithm, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    private static bool TryWriteField(BigInteger value, Span<byte> field)
    {
        if (value.Sign <= 0 || value.GetByteCount(isUnsigned: true) > field.Length)
        {
            return false;
        }

        var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        bytes.CopyTo(field[(field.Length - bytes.Length)..]);

        return true;
    }
}
