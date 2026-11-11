using System.Security.Cryptography;

namespace Surl.Cryptography.Curve448;

/// <summary>
/// The X448 function of RFC 7748 section 5 and the public-key form of section 6.2: a
/// scalar is 56 bytes, clamped as section 5 specifies, and a u-coordinate, public key or
/// shared secret is a 56-byte little-endian u-coordinate on curve448, decoded modulo
/// p = 2^448 - 2^224 - 1 with no bit masked.
/// </summary>
/// <remarks>
/// Constant-time in the scalar: the Montgomery ladder visits all 448 scalar bits with the
/// same operations, the conditional swap is a mask rather than a branch, no array index or
/// loop bound depends on a secret, and the final inversion is a fixed exponentiation.
/// Every secret intermediate is zeroed before returning. The result is returned as
/// computed: rejecting the all-zero shared secret a low-order peer key produces is the
/// caller's (RFC 7748 section 6.2; RFC 8446 section 7.4.2).
/// </remarks>
public static class X448
{
    /// <summary>The length in bytes of a scalar, a u-coordinate and a result.</summary>
    public const int KeySize = 56;

    /// <summary>a24 = (156326 - 2) / 4 = 39081 for curve448 (RFC 7748 section 5).</summary>
    private const uint A24 = 39081;

    /// <summary>The number of scalar bits the ladder visits (RFC 7748 section 5: bits = 448).</summary>
    private const int ScalarBits = 448;

    /// <summary>
    /// Computes the public key X448(<paramref name="privateKey" />, 5) into
    /// <paramref name="publicKey" /> (RFC 7748 section 6.2).
    /// </summary>
    /// <param name="privateKey">The 56-byte private key; it is clamped as it is used.</param>
    /// <param name="publicKey">Receives the 56-byte public key.</param>
    /// <exception cref="ArgumentException">A span is not <see cref="KeySize" /> bytes.</exception>
    public static void ComputePublicKey(ReadOnlySpan<byte> privateKey, Span<byte> publicKey)
    {
        Span<byte> basePoint = stackalloc byte[KeySize];
        basePoint.Clear();
        basePoint[0] = 5;
        ScalarMultiply(privateKey, basePoint, publicKey);
    }

    /// <summary>
    /// Computes X448(<paramref name="scalar" />, <paramref name="uCoordinate" />) into
    /// <paramref name="result" />: clamp the scalar, decode the u-coordinate modulo p, run
    /// the Montgomery ladder and encode x2 / z2 (RFC 7748 section 5).
    /// </summary>
    /// <param name="scalar">The 56-byte scalar (a private key).</param>
    /// <param name="uCoordinate">The 56-byte u-coordinate (a peer's public key).</param>
    /// <param name="result">Receives the 56-byte result (a public key or shared secret).</param>
    /// <exception cref="ArgumentException">A span is not <see cref="KeySize" /> bytes.</exception>
    public static void ScalarMultiply(ReadOnlySpan<byte> scalar, ReadOnlySpan<byte> uCoordinate, Span<byte> result)
    {
        RequireKeySize(scalar.Length, nameof(scalar));
        RequireKeySize(uCoordinate.Length, nameof(uCoordinate));
        RequireKeySize(result.Length, nameof(result));
        Span<byte> clamped = stackalloc byte[KeySize];
        Span<ulong> ladder = stackalloc ulong[5 * Field448.LimbCount];
        Span<ulong> x1 = ladder[..Field448.LimbCount];
        Span<ulong> x2 = ladder.Slice(Field448.LimbCount, Field448.LimbCount);
        Span<ulong> z2 = ladder.Slice(2 * Field448.LimbCount, Field448.LimbCount);
        Span<ulong> x3 = ladder.Slice(3 * Field448.LimbCount, Field448.LimbCount);
        Span<ulong> z3 = ladder.Slice(4 * Field448.LimbCount, Field448.LimbCount);
        try
        {
            scalar.CopyTo(clamped);
            clamped[0] &= 252;
            clamped[KeySize - 1] |= 128;
            Field448.Decode(x1, uCoordinate);
            Field448.SetSmall(x2, 1);
            Field448.SetSmall(z2, 0);
            x1.CopyTo(x3);
            Field448.SetSmall(z3, 1);
            RunLadder(clamped, x1, x2, z2, x3, z3);
            Field448.Invert(z2, z2);
            Field448.Multiply(x2, x2, z2);
            Field448.Encode(result, x2);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clamped);
            Field448.Clear(ladder);
        }
    }

    private static void RequireKeySize(int length, string parameterName)
    {
        if (length != KeySize)
        {
            throw new ArgumentException($"X448 values are {KeySize} bytes; this one is {length}.", parameterName);
        }
    }

    /// <summary>
    /// The Montgomery ladder of RFC 7748 section 5 over bits 447 down to 0 of the clamped
    /// scalar, swapping by mask so the secret bit never picks a branch or an index.
    /// </summary>
    private static void RunLadder(
        ReadOnlySpan<byte> clamped,
        ReadOnlySpan<ulong> x1,
        Span<ulong> x2,
        Span<ulong> z2,
        Span<ulong> x3,
        Span<ulong> z3)
    {
        uint swap = 0;
        for (int bit = ScalarBits - 1; bit >= 0; bit--)
        {
            uint scalarBit = (uint)(clamped[bit >> 3] >> (bit & 7)) & 1u;
            swap ^= scalarBit;
            Field448.ConditionalSwap(x2, x3, swap);
            Field448.ConditionalSwap(z2, z3, swap);
            swap = scalarBit;
            LadderStep(x1, x2, z2, x3, z3);
        }

        Field448.ConditionalSwap(x2, x3, swap);
        Field448.ConditionalSwap(z2, z3, swap);
    }

    /// <summary>One differential add-and-double step, as RFC 7748 section 5 writes it.</summary>
    private static void LadderStep(
        ReadOnlySpan<ulong> x1,
        Span<ulong> x2,
        Span<ulong> z2,
        Span<ulong> x3,
        Span<ulong> z3)
    {
        Span<ulong> scratch = stackalloc ulong[9 * Field448.LimbCount];
        Span<ulong> a = scratch[..Field448.LimbCount];
        Span<ulong> aa = scratch.Slice(Field448.LimbCount, Field448.LimbCount);
        Span<ulong> b = scratch.Slice(2 * Field448.LimbCount, Field448.LimbCount);
        Span<ulong> bb = scratch.Slice(3 * Field448.LimbCount, Field448.LimbCount);
        Span<ulong> e = scratch.Slice(4 * Field448.LimbCount, Field448.LimbCount);
        Span<ulong> c = scratch.Slice(5 * Field448.LimbCount, Field448.LimbCount);
        Span<ulong> d = scratch.Slice(6 * Field448.LimbCount, Field448.LimbCount);
        Span<ulong> da = scratch.Slice(7 * Field448.LimbCount, Field448.LimbCount);
        Span<ulong> cb = scratch.Slice(8 * Field448.LimbCount, Field448.LimbCount);
        try
        {
            Field448.Add(a, x2, z2);
            Field448.Square(aa, a);
            Field448.Subtract(b, x2, z2);
            Field448.Square(bb, b);
            Field448.Subtract(e, aa, bb);
            Field448.Add(c, x3, z3);
            Field448.Subtract(d, x3, z3);
            Field448.Multiply(da, d, a);
            Field448.Multiply(cb, c, b);
            Field448.Add(x3, da, cb);
            Field448.Square(x3, x3);
            Field448.Subtract(z3, da, cb);
            Field448.Square(z3, z3);
            Field448.Multiply(z3, z3, x1);
            Field448.Multiply(x2, aa, bb);
            Field448.SetSmall(a, A24);
            Field448.Multiply(z2, e, a);
            Field448.Add(z2, z2, aa);
            Field448.Multiply(z2, z2, e);
        }
        finally
        {
            Field448.Clear(scratch);
        }
    }
}
