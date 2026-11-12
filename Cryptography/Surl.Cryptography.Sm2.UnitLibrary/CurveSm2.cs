using System.Numerics;

namespace Surl.Cryptography.Sm2;

/// <summary>
/// Elliptic-curve Diffie-Hellman on curveSM2, the SM2 recommended curve of GB/T 32918.5-2017
/// (TLS NamedGroup 41, 0x0029, RFC 8998), in the form TLS 1.3 uses for its elliptic-curve
/// groups: a public key is the uncompressed point 0x04 || x || y (RFC 8446 section 4.2.8.2)
/// and the shared secret is the x-coordinate of the product (RFC 8446 section 7.4.2), every
/// coordinate 32 bytes big-endian.
/// </summary>
/// <remarks>
/// Hand-built on <see cref="BigInteger" /> because the base class library has no SM2 curve on
/// every platform: explicit curves are unavailable on macOS. Not constant-time, which is
/// acceptable for the ephemeral keys of a loopback test server.
/// </remarks>
public static class CurveSm2
{
    /// <summary>The length in bytes of a private key: 32, big-endian.</summary>
    public const int PrivateKeySize = 32;

    /// <summary>The length in bytes of a public key: 0x04, then x and y of 32 bytes each.</summary>
    public const int PublicKeySize = 65;

    /// <summary>The length in bytes of a shared secret: the product's 32-byte x-coordinate.</summary>
    public const int SharedSecretSize = 32;

    /// <summary>The first byte of an uncompressed point (SEC 1 section 2.3.3, RFC 8446 section 4.2.8.2).</summary>
    private const byte UncompressedPointPrefix = 0x04;

    /// <summary>
    /// Computes the public key [d]G of <paramref name="privateKey" /> as an uncompressed point.
    /// </summary>
    /// <param name="privateKey">
    /// The 32-byte big-endian private key; the scalar d used is (value mod (n - 1)) + 1, so any
    /// 32 bytes are a valid key and d lies in [1, n - 1].
    /// </param>
    /// <param name="publicKey">Receives the 65-byte public key 0x04 || x || y.</param>
    /// <exception cref="ArgumentException">A span is not the size its constant names.</exception>
    public static void ComputePublicKey(ReadOnlySpan<byte> privateKey, Span<byte> publicKey)
    {
        RequireSize(privateKey.Length, PrivateKeySize, nameof(privateKey));
        RequireSize(publicKey.Length, PublicKeySize, nameof(publicKey));
        var product = Sm2PointArithmetic.Multiply(ScalarOf(privateKey), CurveSm2Parameters.G);
        _ = Sm2PointArithmetic.TryToAffine(product, out var point);
        publicKey[0] = UncompressedPointPrefix;
        WriteElement(point.X, publicKey.Slice(1, CurveSm2Parameters.ElementSize));
        WriteElement(point.Y, publicKey.Slice(1 + CurveSm2Parameters.ElementSize, CurveSm2Parameters.ElementSize));
    }

    /// <summary>
    /// Computes the shared secret, the x-coordinate of [d]Q for the private key's scalar d and the
    /// peer's point Q.
    /// </summary>
    /// <param name="privateKey">The 32-byte big-endian private key, read as <see cref="ComputePublicKey" /> reads it.</param>
    /// <param name="peerPublicKey">The peer's 65-byte public key 0x04 || x || y.</param>
    /// <param name="sharedSecret">Receives the 32-byte shared secret; untouched when this returns false.</param>
    /// <returns>
    /// False, writing nothing, when <paramref name="peerPublicKey" /> is not 0x04 || x || y with
    /// x and y below p and on the curve, or when the product is the point at infinity.
    /// </returns>
    /// <exception cref="ArgumentException">A span is not the size its constant names.</exception>
    public static bool TryComputeSharedSecret(ReadOnlySpan<byte> privateKey, ReadOnlySpan<byte> peerPublicKey, Span<byte> sharedSecret)
    {
        RequireSize(privateKey.Length, PrivateKeySize, nameof(privateKey));
        RequireSize(peerPublicKey.Length, PublicKeySize, nameof(peerPublicKey));
        RequireSize(sharedSecret.Length, SharedSecretSize, nameof(sharedSecret));
        return TryDecodePoint(peerPublicKey, out var peerPoint)
            && TryComputeSharedSecret(ScalarOf(privateKey), peerPoint, sharedSecret);
    }

    /// <summary>
    /// Writes the x-coordinate of [<paramref name="scalar" />]<paramref name="peerPoint" /> into
    /// <paramref name="sharedSecret" />, or returns false, writing nothing, when the product is
    /// the point at infinity.
    /// </summary>
    /// <param name="scalar">The scalar.</param>
    /// <param name="peerPoint">A finite point on the curve.</param>
    /// <param name="sharedSecret">Receives the 32-byte x-coordinate.</param>
    /// <returns>False when the product is the point at infinity.</returns>
    internal static bool TryComputeSharedSecret(BigInteger scalar, Sm2AffinePoint peerPoint, Span<byte> sharedSecret)
    {
        if (!Sm2PointArithmetic.TryToAffine(Sm2PointArithmetic.Multiply(scalar, peerPoint), out var product))
        {
            return false;
        }

        WriteElement(product.X, sharedSecret);
        return true;
    }

    /// <summary>The scalar a private key stands for: (value mod (n - 1)) + 1, in [1, n - 1].</summary>
    /// <param name="privateKey">The 32-byte big-endian private key.</param>
    /// <returns>The scalar.</returns>
    internal static BigInteger ScalarOf(ReadOnlySpan<byte> privateKey) =>
        (new BigInteger(privateKey, isUnsigned: true, isBigEndian: true) % (CurveSm2Parameters.N - 1)) + 1;

    /// <summary>
    /// Decodes an uncompressed point 0x04 || x || y, accepting it only when x and y are below p
    /// and the point is on the curve (RFC 8446 section 4.2.8.2's validation).
    /// </summary>
    /// <param name="encoded">The 65-byte encoding.</param>
    /// <param name="point">The decoded point; default when this returns false.</param>
    /// <returns>True for a valid point.</returns>
    internal static bool TryDecodePoint(ReadOnlySpan<byte> encoded, out Sm2AffinePoint point)
    {
        point = new Sm2AffinePoint(
            new BigInteger(encoded.Slice(1, CurveSm2Parameters.ElementSize), isUnsigned: true, isBigEndian: true),
            new BigInteger(encoded.Slice(1 + CurveSm2Parameters.ElementSize, CurveSm2Parameters.ElementSize), isUnsigned: true, isBigEndian: true));
        var valid = encoded[0] == UncompressedPointPrefix
            && point.X < CurveSm2Parameters.P
            && point.Y < CurveSm2Parameters.P
            && Sm2PointArithmetic.IsOnCurve(point);
        point = valid ? point : default;
        return valid;
    }

    /// <summary>Writes a value below 2^256 as exactly 32 big-endian bytes, zero-padded on the left.</summary>
    /// <param name="value">The non-negative value.</param>
    /// <param name="destination">The 32-byte destination.</param>
    private static void WriteElement(BigInteger value, Span<byte> destination)
    {
        destination.Clear();
        var length = value.GetByteCount(isUnsigned: true);
        _ = value.TryWriteBytes(destination[(CurveSm2Parameters.ElementSize - length)..], out _, isUnsigned: true, isBigEndian: true);
    }

    /// <summary>Throws when a span is not the size its constant names.</summary>
    /// <param name="length">The span's length.</param>
    /// <param name="expected">The size required.</param>
    /// <param name="parameterName">The parameter's name.</param>
    /// <exception cref="ArgumentException"><paramref name="length" /> is not <paramref name="expected" />.</exception>
    private static void RequireSize(int length, int expected, string parameterName)
    {
        if (length != expected)
        {
            throw new ArgumentException($"Expected {expected} bytes, got {length}.", parameterName);
        }
    }
}
