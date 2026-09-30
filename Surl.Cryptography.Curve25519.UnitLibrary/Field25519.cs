using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Surl.Cryptography.Curve25519;

/// <summary>
/// Arithmetic in GF(2^255 - 19), the field X25519 (RFC 7748) and Ed25519 (RFC 8032) work
/// in. An element is <see cref="LimbCount" /> signed 64-bit limbs of 16 bits each, least
/// significant first, held in a caller's <see cref="Span{T}" /> (usually a
/// <c>stackalloc</c>), so no operation allocates. Outputs may alias inputs.
/// </summary>
/// <remarks>
/// Constant-time: loop bounds and indexes depend only on the limb count or on public
/// constants, never on an element's value, and every conditional move or swap is a mask
/// rather than a branch.
/// </remarks>
public static class Field25519
{
    /// <summary>The number of limbs in one field element.</summary>
    public const int LimbCount = 16;

    /// <summary>The number of bytes in an element's little-endian encoding.</summary>
    public const int EncodedLength = 32;

    /// <summary>Sets <paramref name="element" /> to the small value <paramref name="value" />.</summary>
    /// <param name="element">The element to set.</param>
    /// <param name="value">The value it takes.</param>
    public static void SetSmall(Span<long> element, ushort value)
    {
        element[..LimbCount].Clear();
        element[0] = value;
    }

    /// <summary>
    /// Decodes a 32-byte little-endian value into <paramref name="element" />, ignoring its
    /// top bit (RFC 7748 section 5, <c>decodeUCoordinate</c>; RFC 8032 section 5.1.3 keeps
    /// the sign of x there). A value of p or more is still decoded, and is reduced as it is
    /// used.
    /// </summary>
    /// <param name="element">Receives the decoded element.</param>
    /// <param name="encoded">The <see cref="EncodedLength" />-byte encoding.</param>
    /// <returns>
    /// <c>true</c> when the 255-bit value is below p, the canonical encoding RFC 8032
    /// section 5.1.3 requires of y; <c>false</c> when it is p or more. The check takes the
    /// same steps either way.
    /// </returns>
    public static bool Decode(Span<long> element, ReadOnlySpan<byte> encoded)
    {
        for (int index = 0; index < LimbCount; index++)
        {
            element[index] = encoded[2 * index] | ((long)encoded[(2 * index) + 1] << 8);
        }

        element[LimbCount - 1] &= 0x7FFF;
        Span<long> difference = stackalloc long[LimbCount];
        try
        {
            return SubtractPrime(element, difference) == 1u;
        }
        finally
        {
            Clear(difference);
        }
    }

    /// <summary>
    /// Encodes <paramref name="element" /> as the 32-byte little-endian encoding of its
    /// canonical value, fully reduced modulo p.
    /// </summary>
    /// <param name="encoded">Receives the <see cref="EncodedLength" />-byte encoding.</param>
    /// <param name="element">The element to encode.</param>
    public static void Encode(Span<byte> encoded, ReadOnlySpan<long> element)
    {
        Span<long> reduced = stackalloc long[LimbCount];
        Span<long> subtracted = stackalloc long[LimbCount];
        try
        {
            element[..LimbCount].CopyTo(reduced);
            Carry(reduced);
            Carry(reduced);
            Carry(reduced);
            SubtractPrimeIfNotBelow(reduced, subtracted);
            SubtractPrimeIfNotBelow(reduced, subtracted);
            for (int index = 0; index < LimbCount; index++)
            {
                encoded[2 * index] = (byte)reduced[index];
                encoded[(2 * index) + 1] = (byte)(reduced[index] >> 8);
            }
        }
        finally
        {
            Clear(reduced);
            Clear(subtracted);
        }
    }

    /// <summary>Sets <paramref name="result" /> to <paramref name="left" /> + <paramref name="right" />.</summary>
    /// <param name="result">Receives the sum.</param>
    /// <param name="left">The first addend.</param>
    /// <param name="right">The second addend.</param>
    public static void Add(Span<long> result, ReadOnlySpan<long> left, ReadOnlySpan<long> right)
    {
        for (int index = 0; index < LimbCount; index++)
        {
            result[index] = left[index] + right[index];
        }
    }

    /// <summary>Sets <paramref name="result" /> to <paramref name="left" /> - <paramref name="right" />.</summary>
    /// <param name="result">Receives the difference.</param>
    /// <param name="left">The minuend.</param>
    /// <param name="right">The subtrahend.</param>
    public static void Subtract(Span<long> result, ReadOnlySpan<long> left, ReadOnlySpan<long> right)
    {
        for (int index = 0; index < LimbCount; index++)
        {
            result[index] = left[index] - right[index];
        }
    }

    /// <summary>Sets <paramref name="result" /> to -<paramref name="value" />.</summary>
    /// <param name="result">Receives the negation.</param>
    /// <param name="value">The element to negate.</param>
    public static void Negate(Span<long> result, ReadOnlySpan<long> value)
    {
        for (int index = 0; index < LimbCount; index++)
        {
            result[index] = -value[index];
        }
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="left" /> * <paramref name="right" />,
    /// folding the high half back in with 2^256 = 38 (mod p) and carrying twice.
    /// </summary>
    /// <param name="result">Receives the product.</param>
    /// <param name="left">The first factor.</param>
    /// <param name="right">The second factor.</param>
    public static void Multiply(Span<long> result, ReadOnlySpan<long> left, ReadOnlySpan<long> right)
    {
        Span<long> product = stackalloc long[(2 * LimbCount) - 1];
        try
        {
            product.Clear();
            for (int leftIndex = 0; leftIndex < LimbCount; leftIndex++)
            {
                long leftLimb = left[leftIndex];
                Span<long> row = product.Slice(leftIndex, LimbCount);
                for (int rightIndex = 0; rightIndex < LimbCount; rightIndex++)
                {
                    row[rightIndex] += leftLimb * right[rightIndex];
                }
            }

            Reduce(result, product);
        }
        finally
        {
            Clear(product);
        }
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="value" /> squared, forming each
    /// cross product once and doubling it.
    /// </summary>
    /// <param name="result">Receives the square.</param>
    /// <param name="value">The element to square.</param>
    public static void Square(Span<long> result, ReadOnlySpan<long> value)
    {
        Span<long> product = stackalloc long[(2 * LimbCount) - 1];
        try
        {
            product.Clear();
            for (int leftIndex = 0; leftIndex < LimbCount; leftIndex++)
            {
                long leftLimb = value[leftIndex];
                product[2 * leftIndex] += leftLimb * leftLimb;
                long doubled = 2 * leftLimb;
                for (int rightIndex = leftIndex + 1; rightIndex < LimbCount; rightIndex++)
                {
                    product[leftIndex + rightIndex] += doubled * value[rightIndex];
                }
            }

            Reduce(result, product);
        }
        finally
        {
            Clear(product);
        }
    }

    /// <summary>
    /// Sets <paramref name="result" /> to the inverse of <paramref name="value" />, computed
    /// as value^(p - 2) by a fixed square-and-multiply chain; zero maps to zero.
    /// </summary>
    /// <param name="result">Receives the inverse.</param>
    /// <param name="value">The element to invert.</param>
    public static void Invert(Span<long> result, ReadOnlySpan<long> value)
    {
        // p - 2 = 2^255 - 21: bits 254 down to 0 are all set except bits 2 and 4.
        RaiseToFixedPower(result, value, 254, 2, 4);
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="value" />^((p - 5) / 8), the power
    /// RFC 8032 section 5.1.3 uses to take a square root, by a fixed square-and-multiply
    /// chain.
    /// </summary>
    /// <param name="result">Receives the power.</param>
    /// <param name="value">The element to raise.</param>
    public static void PowerPMinus5Over8(Span<long> result, ReadOnlySpan<long> value)
    {
        // (p - 5) / 8 = 2^252 - 3: bits 251 down to 0 are all set except bit 1. The second
        // skipped bit repeats the first, so the chain has a single exception.
        RaiseToFixedPower(result, value, 251, 1, 1);
    }

    /// <summary>
    /// Returns whether <paramref name="left" /> and <paramref name="right" /> are the same
    /// field element, comparing their canonical encodings in fixed time.
    /// </summary>
    /// <param name="left">The first element.</param>
    /// <param name="right">The second element.</param>
    /// <returns><c>true</c> when both reduce to the same value modulo p.</returns>
    public static bool AreEqual(ReadOnlySpan<long> left, ReadOnlySpan<long> right)
    {
        Span<byte> leftEncoding = stackalloc byte[EncodedLength];
        Span<byte> rightEncoding = stackalloc byte[EncodedLength];
        try
        {
            Encode(leftEncoding, left);
            Encode(rightEncoding, right);
            return CryptographicOperations.FixedTimeEquals(leftEncoding, rightEncoding);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(leftEncoding);
            CryptographicOperations.ZeroMemory(rightEncoding);
        }
    }

    /// <summary>
    /// Returns the lowest bit of the canonical value of <paramref name="element" />
    /// (RFC 8032's "x mod 2", the sign of an x-coordinate), without a branch.
    /// </summary>
    /// <param name="element">The element whose parity is read.</param>
    /// <returns><c>1</c> when the canonical value is odd, otherwise <c>0</c>.</returns>
    public static uint Parity(ReadOnlySpan<long> element)
    {
        Span<byte> encoding = stackalloc byte[EncodedLength];
        try
        {
            Encode(encoding, element);
            return encoding[0] & 1u;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encoding);
        }
    }

    /// <summary>
    /// Swaps <paramref name="left" /> and <paramref name="right" /> when the lowest bit of
    /// <paramref name="bit" /> is <c>1</c>, by masking, touching every limb either way.
    /// </summary>
    /// <param name="left">The first element.</param>
    /// <param name="right">The second element.</param>
    /// <param name="bit">The secret choice; only its lowest bit is read.</param>
    public static void ConditionalSwap(Span<long> left, Span<long> right, uint bit)
    {
        long mask = -(long)(bit & 1u);
        for (int index = 0; index < LimbCount; index++)
        {
            long difference = mask & (left[index] ^ right[index]);
            left[index] ^= difference;
            right[index] ^= difference;
        }
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="whenOne" /> when the lowest bit of
    /// <paramref name="bit" /> is <c>1</c> and to <paramref name="whenZero" /> otherwise,
    /// by masking, reading every limb of both either way.
    /// </summary>
    /// <param name="result">Receives the chosen element.</param>
    /// <param name="whenZero">The element chosen when the bit is <c>0</c>.</param>
    /// <param name="whenOne">The element chosen when the bit is <c>1</c>.</param>
    /// <param name="bit">The secret choice; only its lowest bit is read.</param>
    public static void ConditionalSelect(Span<long> result, ReadOnlySpan<long> whenZero, ReadOnlySpan<long> whenOne, uint bit)
    {
        long mask = -(long)(bit & 1u);
        for (int index = 0; index < LimbCount; index++)
        {
            result[index] = whenZero[index] ^ (mask & (whenZero[index] ^ whenOne[index]));
        }
    }

    /// <summary>Zeroes limbs that held secret material.</summary>
    /// <param name="element">The limbs to zero.</param>
    public static void Clear(Span<long> element) =>
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(element));

    /// <summary>
    /// Raises <paramref name="value" /> to the public exponent whose bits run from
    /// <paramref name="topBit" /> down to 0, all set except <paramref name="clearBit" /> and
    /// <paramref name="otherClearBit" />. The chain's shape depends only on those constants.
    /// </summary>
    private static void RaiseToFixedPower(Span<long> result, ReadOnlySpan<long> value, int topBit, int clearBit, int otherClearBit)
    {
        Span<long> power = stackalloc long[LimbCount];
        try
        {
            value[..LimbCount].CopyTo(power);
            for (int bit = topBit - 1; bit >= 0; bit--)
            {
                Square(power, power);
                if (bit != clearBit && bit != otherClearBit)
                {
                    Multiply(power, power, value);
                }
            }

            power.CopyTo(result);
        }
        finally
        {
            Clear(power);
        }
    }

    /// <summary>
    /// Folds a 31-limb product into <paramref name="result" /> with 2^256 = 38 (mod p) and
    /// carries twice.
    /// </summary>
    private static void Reduce(Span<long> result, Span<long> product)
    {
        for (int index = 0; index < LimbCount - 1; index++)
        {
            product[index] += 38 * product[index + LimbCount];
        }

        product[..LimbCount].CopyTo(result);
        Carry(result);
        Carry(result);
    }

    /// <summary>
    /// Brings every limb into 0 to 2^16 - 1 plus a carry into the lowest limb, folding the
    /// carry out of the top limb back in with 2^256 = 38 (mod p).
    /// </summary>
    private static void Carry(Span<long> element)
    {
        for (int index = 0; index < LimbCount; index++)
        {
            long carry = element[index] >> 16;
            element[index] -= carry << 16;
            int next = (index + 1) % LimbCount;
            element[next] += carry * (next == 0 ? 38 : 1);
        }
    }

    /// <summary>
    /// Replaces a carried <paramref name="element" /> with element - p when that is not
    /// negative, choosing by mask; <paramref name="scratch" /> receives the difference.
    /// </summary>
    private static void SubtractPrimeIfNotBelow(Span<long> element, Span<long> scratch)
    {
        uint borrow = SubtractPrime(element, scratch);
        ConditionalSwap(element, scratch, 1u - borrow);
    }

    /// <summary>
    /// Sets <paramref name="difference" /> to <paramref name="element" /> - p, limb by limb
    /// with the borrow propagated, for an element whose limbs are each 0 to 2^16 - 1.
    /// </summary>
    /// <returns><c>1</c> when the element is below p (the subtraction borrowed), otherwise <c>0</c>.</returns>
    private static uint SubtractPrime(ReadOnlySpan<long> element, Span<long> difference)
    {
        difference[0] = element[0] - 0xFFED;
        for (int index = 1; index < LimbCount - 1; index++)
        {
            difference[index] = element[index] - 0xFFFF - ((difference[index - 1] >> 16) & 1);
            difference[index - 1] &= 0xFFFF;
        }

        difference[LimbCount - 1] = element[LimbCount - 1] - 0x7FFF - ((difference[LimbCount - 2] >> 16) & 1);
        difference[LimbCount - 2] &= 0xFFFF;
        return (uint)((difference[LimbCount - 1] >> 16) & 1);
    }
}
