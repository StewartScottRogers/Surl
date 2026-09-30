using System.Security.Cryptography;
using Surl.Cryptography.Curve25519;

namespace Surl.Cryptography.Ed25519;

/// <summary>
/// Ed25519 signatures (RFC 8032 section 5.1): a private key is a 32-byte seed, a public
/// key is a 32-byte encoded point, and a signature is 64 bytes, R followed by S. Signing
/// is deterministic, so it needs no randomness; SHA-512 comes from the base class
/// library.
/// </summary>
/// <remarks>
/// Key derivation and signing are constant-time in the seed and the secret nonce:
/// scalar multiplication visits all 256 bits with the same operations and swaps by mask,
/// scalar reduction has fixed loop bounds, and every secret intermediate is zeroed before
/// returning. <see cref="Verify" /> works on public data only and is not constant-time.
/// Verification is cofactorless, checking [S]B = R + [k]A by comparing encodings, which
/// RFC 8032 section 5.1.7 permits, and rejects S &gt;= L and a public key that does not
/// decode (section 5.1.3), a non-canonical y included.
/// </remarks>
public static class Ed25519
{
    /// <summary>The length in bytes of a seed, the private key.</summary>
    public const int SeedSize = 32;

    /// <summary>The length in bytes of a public key.</summary>
    public const int PublicKeySize = 32;

    /// <summary>The length in bytes of a signature.</summary>
    public const int SignatureSize = 64;

    private const int HashSize = 64;

    /// <summary>
    /// Returns the public key of <paramref name="seed" /> (RFC 8032 section 5.1.5).
    /// </summary>
    /// <param name="seed">The <see cref="SeedSize" />-byte private key.</param>
    /// <returns>The <see cref="PublicKeySize" />-byte public key.</returns>
    /// <exception cref="ArgumentException"><paramref name="seed" /> is not <see cref="SeedSize" /> bytes.</exception>
    public static byte[] ComputePublicKey(ReadOnlySpan<byte> seed)
    {
        RequireSize(seed.Length, SeedSize, nameof(seed));
        byte[] publicKey = new byte[PublicKeySize];
        Span<byte> expanded = stackalloc byte[HashSize];
        try
        {
            ExpandSeed(seed, expanded);
            MultiplyBaseAndEncode(publicKey, expanded[..Scalar25519.EncodedLength]);
            return publicKey;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expanded);
        }
    }

    /// <summary>
    /// Returns the signature of <paramref name="message" /> under <paramref name="seed" />
    /// (RFC 8032 section 5.1.6).
    /// </summary>
    /// <param name="seed">The <see cref="SeedSize" />-byte private key.</param>
    /// <param name="message">The message to sign, of any length.</param>
    /// <returns>The <see cref="SignatureSize" />-byte signature, R followed by S.</returns>
    /// <exception cref="ArgumentException"><paramref name="seed" /> is not <see cref="SeedSize" /> bytes.</exception>
    public static byte[] Sign(ReadOnlySpan<byte> seed, ReadOnlySpan<byte> message)
    {
        RequireSize(seed.Length, SeedSize, nameof(seed));
        byte[] signature = new byte[SignatureSize];
        Span<byte> secrets = stackalloc byte[(2 * HashSize) + (2 * Scalar25519.EncodedLength)];
        Span<byte> expanded = secrets[..HashSize];
        Span<byte> hash = secrets.Slice(HashSize, HashSize);
        Span<byte> nonce = secrets.Slice(2 * HashSize, Scalar25519.EncodedLength);
        Span<byte> challenge = secrets.Slice((2 * HashSize) + Scalar25519.EncodedLength, Scalar25519.EncodedLength);
        Span<byte> publicKey = stackalloc byte[PublicKeySize];
        Span<byte> encodedR = signature.AsSpan(0, Edwards25519.EncodedLength);
        try
        {
            ExpandSeed(seed, expanded);
            ReadOnlySpan<byte> secretScalar = expanded[..Scalar25519.EncodedLength];
            MultiplyBaseAndEncode(publicKey, secretScalar);
            HashToScalar(nonce, hash, expanded[Scalar25519.EncodedLength..], [], message);
            MultiplyBaseAndEncode(encodedR, nonce);
            HashToScalar(challenge, hash, encodedR, publicKey, message);
            Scalar25519.MultiplyAdd(signature.AsSpan(Edwards25519.EncodedLength), challenge, secretScalar, nonce);
            return signature;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secrets);
        }
    }

    /// <summary>
    /// Verifies <paramref name="signature" /> over <paramref name="message" /> against
    /// <paramref name="publicKey" /> (RFC 8032 section 5.1.7).
    /// </summary>
    /// <param name="publicKey">The <see cref="PublicKeySize" />-byte public key.</param>
    /// <param name="message">The signed message.</param>
    /// <param name="signature">The <see cref="SignatureSize" />-byte signature.</param>
    /// <returns>
    /// <c>true</c> when the signature is valid; <c>false</c> when S is not below the group
    /// order L, when the public key does not decode to a point, or when
    /// [S]B differs from R + [k]A.
    /// </returns>
    /// <exception cref="ArgumentException">A key or signature has the wrong length.</exception>
    public static bool Verify(ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature)
    {
        RequireSize(publicKey.Length, PublicKeySize, nameof(publicKey));
        RequireSize(signature.Length, SignatureSize, nameof(signature));
        ReadOnlySpan<byte> encodedR = signature[..Edwards25519.EncodedLength];
        ReadOnlySpan<byte> s = signature[Edwards25519.EncodedLength..];
        Span<long> negatedKey = stackalloc long[Edwards25519.PointLength];
        if (!Scalar25519.IsBelowOrder(s) || !Edwards25519.TryDecode(negatedKey, publicKey))
        {
            return false;
        }

        Span<byte> hash = stackalloc byte[HashSize];
        Span<byte> challenge = stackalloc byte[Scalar25519.EncodedLength];
        HashToScalar(challenge, hash, encodedR, publicKey, message);
        Edwards25519.Negate(negatedKey);
        Span<long> sum = stackalloc long[Edwards25519.PointLength];
        Span<long> product = stackalloc long[Edwards25519.PointLength];
        Edwards25519.ScalarMultiplyBase(sum, s);
        Edwards25519.ScalarMultiply(product, negatedKey, challenge);
        Edwards25519.Add(sum, product);
        Span<byte> expectedR = stackalloc byte[Edwards25519.EncodedLength];
        Edwards25519.Encode(expectedR, sum);
        return CryptographicOperations.FixedTimeEquals(expectedR, encodedR);
    }

    private static void RequireSize(int length, int size, string parameterName)
    {
        if (length != size)
        {
            throw new ArgumentException($"This Ed25519 value is {size} bytes; the one given is {length}.", parameterName);
        }
    }

    /// <summary>
    /// Hashes the seed with SHA-512 and prunes the lower half into the secret scalar
    /// (RFC 8032 section 5.1.5, steps 1 and 2); the upper half is the nonce prefix.
    /// </summary>
    private static void ExpandSeed(ReadOnlySpan<byte> seed, Span<byte> expanded)
    {
        SHA512.HashData(seed, expanded);
        expanded[0] &= 248;
        expanded[Scalar25519.EncodedLength - 1] &= 127;
        expanded[Scalar25519.EncodedLength - 1] |= 64;
    }

    /// <summary>Encodes [<paramref name="scalar" />]B into <paramref name="encoded" />.</summary>
    private static void MultiplyBaseAndEncode(Span<byte> encoded, ReadOnlySpan<byte> scalar)
    {
        Span<long> point = stackalloc long[Edwards25519.PointLength];
        try
        {
            Edwards25519.ScalarMultiplyBase(point, scalar);
            Edwards25519.Encode(encoded, point);
        }
        finally
        {
            Field25519.Clear(point);
        }
    }

    /// <summary>
    /// Sets <paramref name="scalar" /> to SHA-512(<paramref name="first" /> ||
    /// <paramref name="second" /> || <paramref name="message" />) modulo L, using
    /// <paramref name="hash" /> for the digest.
    /// </summary>
    private static void HashToScalar(
        Span<byte> scalar,
        Span<byte> hash,
        ReadOnlySpan<byte> first,
        ReadOnlySpan<byte> second,
        ReadOnlySpan<byte> message)
    {
        using IncrementalHash sha512 = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
        sha512.AppendData(first);
        sha512.AppendData(second);
        sha512.AppendData(message);
        sha512.GetHashAndReset(hash);
        Scalar25519.Reduce(scalar, hash);
    }
}
