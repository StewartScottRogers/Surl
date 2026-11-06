using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Surl.Cryptography.Ed25519;

/// <summary>
/// Arithmetic on Ed25519 scalars modulo the prime group order L = 2^252 +
/// 27742317777372353535851937790883648493 (RFC 8032 section 5.1). A scalar is 32
/// little-endian bytes. Reduction works on 64 signed 64-bit limbs of 8 bits each with
/// fixed loop bounds and no secret-dependent branch or index, so it is constant-time in
/// the scalars it reduces.
/// </summary>
internal static class Scalar25519
{
    /// <summary>The number of bytes in a scalar's encoding.</summary>
    public const int EncodedLength = 32;

    /// <summary>The number of bytes in the SHA-512 output a scalar is reduced from.</summary>
    public const int WideLength = 64;

    /// <summary>L, little-endian.</summary>
    private static ReadOnlySpan<byte> Order =>
    [
        0xED, 0xD3, 0xF5, 0x5C, 0x1A, 0x63, 0x12, 0x58, 0xD6, 0x9C, 0xF7, 0xA2, 0xDE, 0xF9, 0xDE, 0x14,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10,
    ];

    /// <summary>
    /// Sets <paramref name="result" /> to the 64-byte little-endian integer
    /// <paramref name="wide" /> modulo L.
    /// </summary>
    public static void Reduce(Span<byte> result, ReadOnlySpan<byte> wide)
    {
        Span<long> limbs = stackalloc long[WideLength];
        try
        {
            for (int index = 0; index < WideLength; index++)
            {
                limbs[index] = wide[index];
            }

            ReduceLimbs(result, limbs);
        }
        finally
        {
            ClearLimbs(limbs);
        }
    }

    /// <summary>
    /// Sets <paramref name="result" /> to (<paramref name="left" /> *
    /// <paramref name="right" /> + <paramref name="addend" />) modulo L, the
    /// S = (r + k * s) mod L of RFC 8032 section 5.1.6.
    /// </summary>
    public static void MultiplyAdd(
        Span<byte> result,
        ReadOnlySpan<byte> left,
        ReadOnlySpan<byte> right,
        ReadOnlySpan<byte> addend)
    {
        Span<long> limbs = stackalloc long[WideLength];
        try
        {
            limbs.Clear();
            for (int leftIndex = 0; leftIndex < EncodedLength; leftIndex++)
            {
                limbs[leftIndex] += addend[leftIndex];
                for (int rightIndex = 0; rightIndex < EncodedLength; rightIndex++)
                {
                    limbs[leftIndex + rightIndex] += (long)left[leftIndex] * right[rightIndex];
                }
            }

            ReduceLimbs(result, limbs);
        }
        finally
        {
            ClearLimbs(limbs);
        }
    }

    /// <summary>
    /// Returns whether the 32-byte little-endian integer <paramref name="scalar" /> is
    /// below L, the canonical-S check of RFC 8032 section 5.1.7. Reads public data only,
    /// so it stops at the first byte that differs.
    /// </summary>
    public static bool IsBelowOrder(ReadOnlySpan<byte> scalar)
    {
        for (int index = EncodedLength - 1; index >= 0; index--)
        {
            if (scalar[index] != Order[index])
            {
                return scalar[index] < Order[index];
            }
        }

        return false;
    }

    /// <summary>
    /// Reduces 64 byte-sized limbs modulo L into 32 bytes: folds limbs 63 down to 32 into
    /// the limbs below them with 2^252 = -(L - 2^252) (mod L), subtracts the multiple of L
    /// that limb 31's top nibble holds, and settles the carries (TweetNaCl's
    /// <c>modL</c>).
    /// </summary>
    private static void ReduceLimbs(Span<byte> result, Span<long> limbs)
    {
        for (int high = WideLength - 1; high >= EncodedLength; high--)
        {
            FoldLimb(limbs, high);
        }

        long carry = 0;
        for (int index = 0; index < EncodedLength; index++)
        {
            limbs[index] += carry - ((limbs[EncodedLength - 1] >> 4) * Order[index]);
            carry = limbs[index] >> 8;
            limbs[index] &= 0xFF;
        }

        for (int index = 0; index < EncodedLength; index++)
        {
            limbs[index] -= carry * Order[index];
        }

        for (int index = 0; index < EncodedLength; index++)
        {
            limbs[index + 1] += limbs[index] >> 8;
            result[index] = (byte)limbs[index];
        }
    }

    /// <summary>
    /// Clears limb <paramref name="high" /> (worth 2^(8 * high)), subtracting its multiple
    /// of L - 2^252 from the twenty limbs starting 32 places below it, carrying as it
    /// goes.
    /// </summary>
    private static void FoldLimb(Span<long> limbs, int high)
    {
        long carry = 0;
        int index = high - EncodedLength;
        for (; index < high - 12; index++)
        {
            limbs[index] += carry - (16 * limbs[high] * Order[index - (high - EncodedLength)]);
            carry = (limbs[index] + 128) >> 8;
            limbs[index] -= carry << 8;
        }

        limbs[index] += carry;
        limbs[high] = 0;
    }

    private static void ClearLimbs(Span<long> limbs) =>
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(limbs));
}
