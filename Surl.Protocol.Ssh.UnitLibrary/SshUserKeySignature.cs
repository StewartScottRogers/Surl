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
/// (RFC 8332, section 3) with the base class library (ADR-0051, decision 6). <c>ssh-rsa</c> and
/// <c>ssh-dss</c> are only ever offered with <c>--allow-weak-ssh-algorithms</c> (BL-221).
/// </summary>
internal static class SshUserKeySignature
{
    private const string RsaSha512 = "rsa-sha2-512";

    /// <summary>
    /// The user-key signature algorithms verified, in ADR-0051 decision 2's host-key order: what
    /// <c>EXT_INFO</c>'s <c>server-sig-algs</c> lists (RFC 8308, section 3.1).
    /// </summary>
    public static IReadOnlyList<string> Algorithms { get; } =
        Array.AsReadOnly([SshEd25519HostKey.Ed25519KeyType, "ecdsa-sha2-nistp256", "ecdsa-sha2-nistp384", "ecdsa-sha2-nistp521", RsaSha512, "rsa-sha2-256"]);

    /// <summary>
    /// The key type a key signing with <paramref name="algorithm"/> has: <c>ssh-rsa</c> for
    /// <c>rsa-sha2-*</c>, the algorithm's own name for <c>ssh-ed25519</c> and <c>ecdsa-sha2-*</c>.
    /// </summary>
    /// <param name="algorithm">The signature algorithm a request names.</param>
    /// <returns>The key type, or <see langword="null"/> when the algorithm is not verified.</returns>
    public static string? KeyTypeFor(string algorithm) =>
        !Algorithms.Contains(algorithm) ? null
        : algorithm.StartsWith("rsa-", StringComparison.Ordinal) ? SshRsaHostKey.RsaKeyType
        : algorithm;

    /// <summary>
    /// Whether <paramref name="signature"/> is <paramref name="algorithm"/>'s signature over
    /// <paramref name="signedData"/> by the key in <paramref name="keyBlob"/>. A blob or
    /// signature that is malformed, names another algorithm or holds a value out of range does
    /// not verify.
    /// </summary>
    /// <param name="algorithm">One of <see cref="Algorithms"/>; the key blob's type is <see cref="KeyTypeFor"/> it.</param>
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

            return algorithm == SshEd25519HostKey.Ed25519KeyType ? VerifiesEd25519(key, rawSignature, signedData)
                : algorithm.StartsWith("rsa-", StringComparison.Ordinal) ? VerifiesRsa(algorithm, key, rawSignature, signedData)
                : VerifiesEcdsa(algorithm, key, rawSignature, signedData);
        }
        catch (Exception exception) when (exception is SshDisconnectRequiredException or CryptographicException)
        {
            return false;
        }
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
        var hash = algorithm == RsaSha512 ? HashAlgorithmName.SHA512 : HashAlgorithmName.SHA256;

        return rsa.VerifyData(signedData, padded, hash, RSASignaturePadding.Pkcs1);
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
