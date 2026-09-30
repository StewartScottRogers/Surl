using Surl.Cryptography.Curve25519;

namespace Surl.Cryptography.Ed25519;

/// <summary>
/// Points on edwards25519, the twisted Edwards curve -x^2 + y^2 = 1 + d x^2 y^2 over
/// GF(2^255 - 19) that Ed25519 signs on (RFC 8032 section 5.1). A point is
/// <see cref="PointLength" /> limbs in a caller's <see cref="Span{T}" />: the extended
/// coordinates X, Y, Z, T (x = X / Z, y = Y / Z, x * y = T / Z), each a
/// <see cref="Field25519" /> element.
/// </summary>
/// <remarks>
/// Addition, scalar multiplication and encoding are constant-time: scalar
/// multiplication visits all 256 scalar bits with the same operations and swaps by mask.
/// <see cref="TryDecode" /> is not; it reads only public keys.
/// </remarks>
internal static class Edwards25519
{
    /// <summary>The number of limbs in one point.</summary>
    public const int PointLength = 4 * Field25519.LimbCount;

    /// <summary>The number of bytes in a point's encoding.</summary>
    public const int EncodedLength = 32;

    /// <summary>The curve constant d = -121665 / 121666.</summary>
    private static readonly long[] CurveConstant = ComputeCurveConstant();

    /// <summary>2 * d, used by point addition.</summary>
    private static readonly long[] DoubledCurveConstant = ComputeDoubledCurveConstant();

    /// <summary>sqrt(-1) = 2^((p - 1) / 4) = 2 * (2^((p - 5) / 8))^2.</summary>
    private static readonly long[] SquareRootOfMinusOne = ComputeSquareRootOfMinusOne();

    /// <summary>The base point B, decoded from its encoding (y = 4 / 5, x positive).</summary>
    private static readonly long[] BasePoint = DecodeBasePoint();

    /// <summary>Sets <paramref name="point" /> to the neutral element (0, 1).</summary>
    public static void SetNeutral(Span<long> point)
    {
        point.Clear();
        Y(point)[0] = 1;
        Z(point)[0] = 1;
    }

    /// <summary>Sets <paramref name="point" /> to its negation (-x, y).</summary>
    public static void Negate(Span<long> point)
    {
        Field25519.Negate(X(point), X(point));
        Field25519.Negate(T(point), T(point));
    }

    /// <summary>
    /// Sets <paramref name="point" /> to <paramref name="point" /> + <paramref name="addend" />
    /// with the complete extended-coordinates formula of RFC 8032 section 5.1.4, which
    /// also doubles; <paramref name="addend" /> may be <paramref name="point" /> itself.
    /// </summary>
    public static void Add(Span<long> point, ReadOnlySpan<long> addend)
    {
        const int Limbs = Field25519.LimbCount;
        Span<long> scratch = stackalloc long[9 * Limbs];
        Span<long> a = scratch[..Limbs];
        Span<long> b = scratch.Slice(Limbs, Limbs);
        Span<long> c = scratch.Slice(2 * Limbs, Limbs);
        Span<long> d = scratch.Slice(3 * Limbs, Limbs);
        Span<long> e = scratch.Slice(4 * Limbs, Limbs);
        Span<long> f = scratch.Slice(5 * Limbs, Limbs);
        Span<long> g = scratch.Slice(6 * Limbs, Limbs);
        Span<long> h = scratch.Slice(7 * Limbs, Limbs);
        Span<long> term = scratch.Slice(8 * Limbs, Limbs);
        try
        {
            Field25519.Subtract(a, Y(point), X(point));
            Field25519.Subtract(term, Y(addend), X(addend));
            Field25519.Multiply(a, a, term);
            Field25519.Add(b, Y(point), X(point));
            Field25519.Add(term, Y(addend), X(addend));
            Field25519.Multiply(b, b, term);
            Field25519.Multiply(c, T(point), T(addend));
            Field25519.Multiply(c, c, DoubledCurveConstant);
            Field25519.Multiply(d, Z(point), Z(addend));
            Field25519.Add(d, d, d);
            Field25519.Subtract(e, b, a);
            Field25519.Subtract(f, d, c);
            Field25519.Add(g, d, c);
            Field25519.Add(h, b, a);
            Field25519.Multiply(X(point), e, f);
            Field25519.Multiply(Y(point), g, h);
            Field25519.Multiply(T(point), e, h);
            Field25519.Multiply(Z(point), f, g);
        }
        finally
        {
            Field25519.Clear(scratch);
        }
    }

    /// <summary>
    /// Sets <paramref name="result" /> to [<paramref name="scalar" />]<paramref name="point" />
    /// for a 32-byte little-endian scalar, by a double-and-add ladder over all 256 bits that
    /// swaps by mask. <paramref name="result" /> must not overlap <paramref name="point" />.
    /// </summary>
    public static void ScalarMultiply(Span<long> result, ReadOnlySpan<long> point, ReadOnlySpan<byte> scalar)
    {
        Span<long> addend = stackalloc long[PointLength];
        try
        {
            point.CopyTo(addend);
            SetNeutral(result);
            for (int bit = (8 * Scalar25519.EncodedLength) - 1; bit >= 0; bit--)
            {
                uint scalarBit = (uint)(scalar[bit >> 3] >> (bit & 7)) & 1u;
                ConditionalSwap(result, addend, scalarBit);
                Add(addend, result);
                Add(result, result);
                ConditionalSwap(result, addend, scalarBit);
            }
        }
        finally
        {
            Field25519.Clear(addend);
        }
    }

    /// <summary>Sets <paramref name="result" /> to [<paramref name="scalar" />]B.</summary>
    public static void ScalarMultiplyBase(Span<long> result, ReadOnlySpan<byte> scalar) =>
        ScalarMultiply(result, BasePoint, scalar);

    /// <summary>
    /// Encodes <paramref name="point" /> as RFC 8032 section 5.1.2 specifies: y
    /// little-endian with the lowest bit of x in the top bit.
    /// </summary>
    public static void Encode(Span<byte> encoded, ReadOnlySpan<long> point)
    {
        Span<long> scratch = stackalloc long[3 * Field25519.LimbCount];
        Span<long> inverse = scratch[..Field25519.LimbCount];
        Span<long> x = scratch.Slice(Field25519.LimbCount, Field25519.LimbCount);
        Span<long> y = scratch.Slice(2 * Field25519.LimbCount, Field25519.LimbCount);
        try
        {
            Field25519.Invert(inverse, Z(point));
            Field25519.Multiply(x, X(point), inverse);
            Field25519.Multiply(y, Y(point), inverse);
            Field25519.Encode(encoded, y);
            encoded[EncodedLength - 1] |= (byte)(Field25519.Parity(x) << 7);
        }
        finally
        {
            Field25519.Clear(scratch);
        }
    }

    /// <summary>
    /// Decodes <paramref name="encoded" /> into <paramref name="point" /> as RFC 8032
    /// section 5.1.3 specifies. Not constant-time: it reads public keys only.
    /// </summary>
    /// <returns>
    /// <c>false</c> when y is not below p, when x^2 = (y^2 - 1) / (d y^2 + 1) has no
    /// square root, or when x is 0 and its sign bit is 1; otherwise <c>true</c>.
    /// </returns>
    public static bool TryDecode(Span<long> point, ReadOnlySpan<byte> encoded)
    {
        if (!Field25519.Decode(Y(point), encoded) || !TryRecoverX(X(point), Y(point)))
        {
            return false;
        }

        uint sign = (uint)encoded[EncodedLength - 1] >> 7;
        Span<long> zero = stackalloc long[Field25519.LimbCount];
        Field25519.SetSmall(zero, 0);
        if (sign == 1 && Field25519.AreEqual(X(point), zero))
        {
            return false;
        }

        if (Field25519.Parity(X(point)) != sign)
        {
            Field25519.Negate(X(point), X(point));
        }

        Field25519.SetSmall(Z(point), 1);
        Field25519.Multiply(T(point), X(point), Y(point));
        return true;
    }

    /// <summary>
    /// Sets <paramref name="x" /> to a square root of u / v, u = y^2 - 1, v = d y^2 + 1,
    /// computed as u v^3 (u v^7)^((p - 5) / 8) and multiplied by sqrt(-1) when that
    /// candidate squares to -u / v (RFC 8032 section 5.1.3, step 3).
    /// </summary>
    /// <returns><c>false</c> when u / v is not a square.</returns>
    private static bool TryRecoverX(Span<long> x, ReadOnlySpan<long> y)
    {
        const int Limbs = Field25519.LimbCount;
        Span<long> scratch = stackalloc long[5 * Limbs];
        Span<long> u = scratch[..Limbs];
        Span<long> v = scratch.Slice(Limbs, Limbs);
        Span<long> vCubed = scratch.Slice(2 * Limbs, Limbs);
        Span<long> check = scratch.Slice(3 * Limbs, Limbs);
        Span<long> one = scratch.Slice(4 * Limbs, Limbs);
        Field25519.SetSmall(one, 1);
        Field25519.Square(u, y);
        Field25519.Multiply(v, u, CurveConstant);
        Field25519.Subtract(u, u, one);
        Field25519.Add(v, v, one);
        Field25519.Square(vCubed, v);
        Field25519.Multiply(vCubed, vCubed, v);
        Field25519.Square(x, vCubed);
        Field25519.Multiply(x, x, v);
        Field25519.Multiply(x, x, u);
        Field25519.PowerPMinus5Over8(x, x);
        Field25519.Multiply(x, x, vCubed);
        Field25519.Multiply(x, x, u);
        Field25519.Square(check, x);
        Field25519.Multiply(check, check, v);
        if (Field25519.AreEqual(check, u))
        {
            return true;
        }

        Field25519.Negate(u, u);
        Field25519.Multiply(x, x, SquareRootOfMinusOne);
        return Field25519.AreEqual(check, u);
    }

    /// <summary>Swaps two points by mask when the lowest bit of <paramref name="bit" /> is <c>1</c>.</summary>
    private static void ConditionalSwap(Span<long> left, Span<long> right, uint bit)
    {
        for (int offset = 0; offset < PointLength; offset += Field25519.LimbCount)
        {
            Field25519.ConditionalSwap(
                left.Slice(offset, Field25519.LimbCount),
                right.Slice(offset, Field25519.LimbCount),
                bit);
        }
    }

    private static Span<long> X(Span<long> point) => point[..Field25519.LimbCount];

    private static Span<long> Y(Span<long> point) => point.Slice(Field25519.LimbCount, Field25519.LimbCount);

    private static Span<long> Z(Span<long> point) => point.Slice(2 * Field25519.LimbCount, Field25519.LimbCount);

    private static Span<long> T(Span<long> point) => point.Slice(3 * Field25519.LimbCount, Field25519.LimbCount);

    private static ReadOnlySpan<long> X(ReadOnlySpan<long> point) => point[..Field25519.LimbCount];

    private static ReadOnlySpan<long> Y(ReadOnlySpan<long> point) => point.Slice(Field25519.LimbCount, Field25519.LimbCount);

    private static ReadOnlySpan<long> Z(ReadOnlySpan<long> point) => point.Slice(2 * Field25519.LimbCount, Field25519.LimbCount);

    private static ReadOnlySpan<long> T(ReadOnlySpan<long> point) => point.Slice(3 * Field25519.LimbCount, Field25519.LimbCount);

    private static long[] ComputeCurveConstant()
    {
        // 121665 = 0x1DB41 and 121666 = 0x1DB42, as two 16-bit limbs each.
        long[] numerator = new long[Field25519.LimbCount];
        long[] denominator = new long[Field25519.LimbCount];
        Field25519.SetSmall(numerator, 0xDB41);
        numerator[1] = 1;
        Field25519.Negate(numerator, numerator);
        Field25519.SetSmall(denominator, 0xDB42);
        denominator[1] = 1;
        Field25519.Invert(denominator, denominator);
        Field25519.Multiply(numerator, numerator, denominator);
        return numerator;
    }

    private static long[] ComputeDoubledCurveConstant()
    {
        long[] doubled = new long[Field25519.LimbCount];
        Field25519.Add(doubled, CurveConstant, CurveConstant);
        return doubled;
    }

    private static long[] ComputeSquareRootOfMinusOne()
    {
        // (p - 1) / 4 = 2 * ((p - 5) / 8) + 1.
        long[] two = new long[Field25519.LimbCount];
        long[] root = new long[Field25519.LimbCount];
        Field25519.SetSmall(two, 2);
        Field25519.PowerPMinus5Over8(root, two);
        Field25519.Square(root, root);
        Field25519.Multiply(root, root, two);
        return root;
    }

    private static long[] DecodeBasePoint()
    {
        byte[] encoded = new byte[EncodedLength];
        encoded.AsSpan().Fill(0x66);
        encoded[0] = 0x58;
        long[] point = new long[PointLength];
        _ = TryDecode(point, encoded);
        return point;
    }
}
