namespace Surl.Cryptography.Curve448;

/// <summary>
/// Arithmetic in GF(p), p = 2^448 - 2^224 - 1, the field of curve448 (RFC 7748 section
/// 4.2). An element is eight unsigned 56-bit limbs, least significant first, so limb i is
/// exactly bytes 7i to 7i + 6 of the 56-byte little-endian encoding.
/// </summary>
/// <remarks>
/// <para>
/// Every operation leaves each limb at most 2^56, which keeps a product of two limbs below
/// 2^112 and a full product column, folded twice, well inside <see cref="UInt128" />.
/// Reduction uses 2^448 = 2^224 + 1 (mod p): a carry out of the top limb is added to limb 0
/// and to limb 4.
/// </para>
/// <para>
/// Constant-time: loop bounds and indexes depend only on the limb count or on the public
/// exponent of <see cref="Invert" />, carries are shifts and masks, and the conditional
/// swap and the final reduction below p are masks rather than branches.
/// </para>
/// </remarks>
internal static class Field448
{
    /// <summary>The number of 56-bit limbs in an element.</summary>
    public const int LimbCount = 8;

    /// <summary>The length in bytes of an encoded element.</summary>
    public const int EncodedLength = 56;

    private const int LimbBits = 56;

    private const int LimbBytes = 7;

    private const int TopFoldLimb = 4;

    private const ulong LimbMask = (1UL << LimbBits) - 1;

    /// <summary>
    /// Sets <paramref name="element" /> to the small value <paramref name="value" />.
    /// </summary>
    /// <param name="element">The element to set.</param>
    /// <param name="value">The value, below 2^56.</param>
    public static void SetSmall(Span<ulong> element, uint value)
    {
        element[..LimbCount].Clear();
        element[0] = value;
    }

    /// <summary>
    /// Decodes a 56-byte little-endian number into <paramref name="element" /> without
    /// masking any bit: a value at or above p is kept as given and reduced by the
    /// arithmetic (RFC 7748 section 5, which masks no bit for X448).
    /// </summary>
    /// <param name="element">Receives the element.</param>
    /// <param name="encoded">The 56-byte encoding.</param>
    public static void Decode(Span<ulong> element, ReadOnlySpan<byte> encoded)
    {
        for (int limb = 0; limb < LimbCount; limb++)
        {
            ulong value = 0;
            for (int index = LimbBytes - 1; index >= 0; index--)
            {
                value = (value << 8) | encoded[(limb * LimbBytes) + index];
            }

            element[limb] = value;
        }
    }

    /// <summary>
    /// Encodes <paramref name="element" /> as its canonical 56-byte little-endian form,
    /// fully reduced below p.
    /// </summary>
    /// <param name="encoded">Receives the 56-byte encoding.</param>
    /// <param name="element">The element to encode.</param>
    public static void Encode(Span<byte> encoded, ReadOnlySpan<ulong> element)
    {
        Span<ulong> reduced = stackalloc ulong[LimbCount];
        element[..LimbCount].CopyTo(reduced);
        for (int pass = 0; pass < 4; pass++)
        {
            CarryOnce(reduced);
        }

        SubtractPrimeIfNotBelow(reduced);
        for (int limb = 0; limb < LimbCount; limb++)
        {
            ulong value = reduced[limb];
            for (int index = 0; index < LimbBytes; index++)
            {
                encoded[(limb * LimbBytes) + index] = (byte)(value >> (8 * index));
            }
        }

        Clear(reduced);
    }

    /// <summary>Sets <paramref name="result" /> to <paramref name="left" /> + <paramref name="right" />.</summary>
    /// <param name="result">Receives the sum; may alias either operand.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static void Add(Span<ulong> result, ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right)
    {
        for (int limb = 0; limb < LimbCount; limb++)
        {
            result[limb] = left[limb] + right[limb];
        }

        Carry(result);
    }

    /// <summary>
    /// Sets <paramref name="result" /> to <paramref name="left" /> - <paramref name="right" />,
    /// computed as left + 2p - right limb by limb so no limb goes negative.
    /// </summary>
    /// <param name="result">Receives the difference; may alias either operand.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static void Subtract(Span<ulong> result, ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right)
    {
        for (int limb = 0; limb < LimbCount; limb++)
        {
            result[limb] = left[limb] + (2 * PrimeLimb(limb)) - right[limb];
        }

        Carry(result);
    }

    /// <summary>Sets <paramref name="result" /> to <paramref name="left" /> * <paramref name="right" />.</summary>
    /// <param name="result">Receives the product; may alias either operand.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public static void Multiply(Span<ulong> result, ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right)
    {
        Span<UInt128> product = stackalloc UInt128[(2 * LimbCount) - 1];
        product.Clear();
        for (int i = 0; i < LimbCount; i++)
        {
            for (int j = 0; j < LimbCount; j++)
            {
                product[i + j] += (UInt128)left[i] * right[j];
            }
        }

        FoldHighColumns(product);
        CarryWide(product[..LimbCount]);
        for (int limb = 0; limb < LimbCount; limb++)
        {
            result[limb] = (ulong)product[limb];
        }

        product.Clear();
        Carry(result);
    }

    /// <summary>Sets <paramref name="result" /> to <paramref name="value" /> squared.</summary>
    /// <param name="result">Receives the square; may alias the operand.</param>
    /// <param name="value">The operand.</param>
    public static void Square(Span<ulong> result, ReadOnlySpan<ulong> value) =>
        Multiply(result, value, value);

    /// <summary>
    /// Sets <paramref name="result" /> to the inverse of <paramref name="value" />, computed
    /// as value^(p - 2); zero maps to zero (RFC 7748 section 5's x_2 * (z_2^(p - 2))).
    /// </summary>
    /// <param name="result">Receives the inverse; may alias the operand.</param>
    /// <param name="value">The operand.</param>
    public static void Invert(Span<ulong> result, ReadOnlySpan<ulong> value)
    {
        Span<ulong> scratch = stackalloc ulong[2 * LimbCount];
        Span<ulong> power = scratch[..LimbCount];
        Span<ulong> baseValue = scratch[LimbCount..];
        value[..LimbCount].CopyTo(baseValue);
        SetSmall(power, 1);

        // p - 2 = 2^448 - 2^224 - 3: every bit from 447 down to 0 is set except bits 224 and 1.
        for (int bit = 447; bit >= 0; bit--)
        {
            Square(power, power);
            if (bit != 224 && bit != 1)
            {
                Multiply(power, power, baseValue);
            }
        }

        power.CopyTo(result);
        Clear(scratch);
    }

    /// <summary>
    /// Swaps <paramref name="left" /> and <paramref name="right" /> when
    /// <paramref name="bit" /> is 1 and leaves them when it is 0, by mask rather than branch.
    /// </summary>
    /// <param name="left">The first element.</param>
    /// <param name="right">The second element.</param>
    /// <param name="bit">0 or 1.</param>
    public static void ConditionalSwap(Span<ulong> left, Span<ulong> right, uint bit)
    {
        ulong mask = 0UL - bit;
        for (int limb = 0; limb < LimbCount; limb++)
        {
            ulong difference = mask & (left[limb] ^ right[limb]);
            left[limb] ^= difference;
            right[limb] ^= difference;
        }
    }

    /// <summary>Zeroes <paramref name="elements" />, so no secret is left on the stack.</summary>
    /// <param name="elements">The limbs to zero.</param>
    public static void Clear(Span<ulong> elements) => elements.Clear();

    /// <summary>Limb <paramref name="limb" /> of p: 2^56 - 1, except 2^56 - 2 at limb 4.</summary>
    private static ulong PrimeLimb(int limb) =>
        LimbMask - (limb == TopFoldLimb ? 1UL : 0UL);

    /// <summary>
    /// Brings every limb to at most 2^56 after an addition, a subtraction or a product.
    /// </summary>
    private static void Carry(Span<ulong> element)
    {
        CarryOnce(element);
        CarryOnce(element);
    }

    /// <summary>
    /// One carry pass from limb 0 to limb 7, folding the carry out of limb 7 (a multiple of
    /// 2^448) back into limbs 0 and 4.
    /// </summary>
    private static void CarryOnce(Span<ulong> element)
    {
        for (int limb = 0; limb < LimbCount - 1; limb++)
        {
            element[limb + 1] += element[limb] >> LimbBits;
            element[limb] &= LimbMask;
        }

        ulong top = element[LimbCount - 1] >> LimbBits;
        element[LimbCount - 1] &= LimbMask;
        element[0] += top;
        element[TopFoldLimb] += top;
    }

    /// <summary>
    /// Folds product columns 14 down to 8 (column k weighs 2^(56k)) into columns k - 8 and
    /// k - 4, because 2^448 = 2^224 + 1 (mod p). Columns 12 to 14 land in 8 to 10, which are
    /// folded after them.
    /// </summary>
    private static void FoldHighColumns(Span<UInt128> product)
    {
        for (int column = product.Length - 1; column >= LimbCount; column--)
        {
            product[column - LimbCount] += product[column];
            product[column - TopFoldLimb] += product[column];
            product[column] = 0;
        }
    }

    /// <summary>
    /// Two carry passes over the eight wide columns, folding the carry out of the top
    /// column into columns 0 and 4, so every column fits a limb.
    /// </summary>
    private static void CarryWide(Span<UInt128> columns)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            for (int column = 0; column < LimbCount - 1; column++)
            {
                columns[column + 1] += columns[column] >> LimbBits;
                columns[column] &= LimbMask;
            }

            UInt128 top = columns[LimbCount - 1] >> LimbBits;
            columns[LimbCount - 1] &= LimbMask;
            columns[0] += top;
            columns[TopFoldLimb] += top;
        }
    }

    /// <summary>
    /// Replaces a value below 2^448 (every limb below 2^56) by value - p when it is at least
    /// p, by mask: such a value is below 2p, so one subtraction reduces it fully.
    /// </summary>
    private static void SubtractPrimeIfNotBelow(Span<ulong> element)
    {
        Span<ulong> difference = stackalloc ulong[LimbCount];
        ulong borrow = 0;
        for (int limb = 0; limb < LimbCount; limb++)
        {
            ulong value = element[limb] - PrimeLimb(limb) - borrow;
            borrow = value >> 63;
            difference[limb] = value & LimbMask;
        }

        // borrow is 1 when the value is below p (keep it) and 0 otherwise (take the difference).
        ulong takeDifference = borrow - 1;
        for (int limb = 0; limb < LimbCount; limb++)
        {
            element[limb] = (difference[limb] & takeDifference) | (element[limb] & ~takeDifference);
        }

        Clear(difference);
    }
}
