namespace Surl.Cryptography.Curve25519;

[TestClass]
public sealed class Field25519Tests
{
    /// <summary>p = 2^255 - 19, little-endian (RFC 7748 section 4.1).</summary>
    private const string Prime = "edffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f";

    /// <summary>p - 1, the largest canonical value.</summary>
    private const string PrimeMinusOne = "ecffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f";

    /// <summary>The first input u-coordinate of RFC 7748 section 5.2.</summary>
    private const string Rfc7748FirstUCoordinate = "e6db6867583030db3594c1a424b15f7c726624ec26b3353b10a903a6d0ab1c4c";

    /// <summary>Alice's public key from RFC 7748 section 6.1.</summary>
    private const string Rfc7748AlicePublicKey = "8520f0098930a754748b7ddcb43ef75a0dbf3a0d26381af4eba4a98eaa9b4e6a";

    private const string One = "0100000000000000000000000000000000000000000000000000000000000000";

    private const string Zero = "0000000000000000000000000000000000000000000000000000000000000000";

    [TestMethod]
    [DataRow("0200000000000000000000000000000000000000000000000000000000000000", DisplayName = "2")]
    [DataRow("0900000000000000000000000000000000000000000000000000000000000000", DisplayName = "9, the base point's u (RFC 7748 section 4.1)")]
    [DataRow(PrimeMinusOne, DisplayName = "p - 1")]
    [DataRow(Rfc7748FirstUCoordinate, DisplayName = "RFC 7748 section 5.2's first u-coordinate")]
    [DataRow(Rfc7748AlicePublicKey, DisplayName = "RFC 7748 section 6.1's Alice public key")]
    public void Invert_NonZeroValue_TimesTheValueIsOne(string encoded)
    {
        Span<long> value = Decoded(encoded);
        Span<long> inverse = stackalloc long[Field25519.LimbCount];

        Field25519.Invert(inverse, value);
        Field25519.Multiply(inverse, inverse, value);

        Assert.AreEqual(One, Encoded(inverse));
    }

    [TestMethod]
    public void Invert_Zero_IsZero()
    {
        Span<long> value = stackalloc long[Field25519.LimbCount];
        Field25519.SetSmall(value, 0);

        Field25519.Invert(value, value);

        Assert.AreEqual(Zero, Encoded(value));
    }

    [TestMethod]
    [DataRow(Prime, DisplayName = "p")]
    [DataRow("eeffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f", DisplayName = "p + 1")]
    [DataRow("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f", DisplayName = "2^255 - 1")]
    [DataRow("edffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff", DisplayName = "p with the ignored top bit set")]
    public void Decode_ValueOfPOrMore_ReportsNonCanonical(string encoded)
    {
        Span<long> element = stackalloc long[Field25519.LimbCount];

        Assert.IsFalse(Field25519.Decode(element, Convert.FromHexString(encoded)));
    }

    [TestMethod]
    [DataRow(Zero, DisplayName = "0")]
    [DataRow(One, DisplayName = "1")]
    [DataRow(PrimeMinusOne, DisplayName = "p - 1")]
    [DataRow("edfffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe7f", DisplayName = "p with its top 16-bit limb one less")]
    [DataRow(Rfc7748FirstUCoordinate, DisplayName = "RFC 7748 section 5.2's first u-coordinate")]
    [DataRow(Rfc7748AlicePublicKey, DisplayName = "RFC 7748 section 6.1's Alice public key")]
    public void Decode_CanonicalValue_ReportsCanonicalAndEncodesBackUnchanged(string encoded)
    {
        Span<long> element = stackalloc long[Field25519.LimbCount];

        bool isCanonical = Field25519.Decode(element, Convert.FromHexString(encoded));

        Assert.IsTrue(isCanonical);
        Assert.AreEqual(encoded, Encoded(element));
    }

    [TestMethod]
    public void Encode_P_ReducesToZero() =>
        Assert.AreEqual(Zero, Encoded(Decoded(Prime)));

    [TestMethod]
    public void Decode_TopBitSet_IgnoresIt() =>
        Assert.AreEqual(
            "e5210f12786811d3f4b7959d0538ae2c31dbe7106fc03c3efc4cd549c715a413",
            Encoded(Decoded("e5210f12786811d3f4b7959d0538ae2c31dbe7106fc03c3efc4cd549c715a493")));

    [TestMethod]
    public void Subtract_OneFromZero_IsPMinusOne()
    {
        Span<long> zero = Decoded(Zero);
        Span<long> one = Decoded(One);

        Field25519.Subtract(zero, zero, one);

        Assert.AreEqual(PrimeMinusOne, Encoded(zero));
    }

    [TestMethod]
    public void Add_OneToPMinusOne_IsZero()
    {
        Span<long> value = Decoded(PrimeMinusOne);

        Field25519.Add(value, value, Decoded(One));

        Assert.AreEqual(Zero, Encoded(value));
    }

    [TestMethod]
    public void Negate_Value_AddsToZero()
    {
        Span<long> value = Decoded(Rfc7748FirstUCoordinate);
        Span<long> negation = stackalloc long[Field25519.LimbCount];

        Field25519.Negate(negation, value);
        Field25519.Add(negation, negation, value);

        Assert.AreEqual(Zero, Encoded(negation));
    }

    [TestMethod]
    public void Square_Value_EqualsMultiplyByItself()
    {
        Span<long> value = Decoded(Rfc7748AlicePublicKey);
        Span<long> square = stackalloc long[Field25519.LimbCount];
        Span<long> product = stackalloc long[Field25519.LimbCount];

        Field25519.Square(square, value);
        Field25519.Multiply(product, value, value);

        Assert.IsTrue(Field25519.AreEqual(square, product));
    }

    [TestMethod]
    public void Multiply_PMinusOneByItself_IsOne()
    {
        Span<long> value = Decoded(PrimeMinusOne);

        Field25519.Multiply(value, value, value);

        Assert.AreEqual(One, Encoded(value));
    }

    // v^((p-5)/8) raised to the 8th is v^(p-5); times v^4 it is v^(p-1) = 1 for non-zero v
    // (Fermat), which pins the power RFC 8032 section 5.1.3 takes.
    [TestMethod]
    [DataRow("0900000000000000000000000000000000000000000000000000000000000000", DisplayName = "9")]
    [DataRow(PrimeMinusOne, DisplayName = "p - 1")]
    [DataRow(Rfc7748FirstUCoordinate, DisplayName = "RFC 7748 section 5.2's first u-coordinate")]
    public void PowerPMinus5Over8_Value_EighthPowerTimesFourthPowerIsOne(string encoded)
    {
        Span<long> value = Decoded(encoded);
        Span<long> power = stackalloc long[Field25519.LimbCount];
        Span<long> valueToTheFourth = stackalloc long[Field25519.LimbCount];

        Field25519.PowerPMinus5Over8(power, value);
        Field25519.Square(power, power);
        Field25519.Square(power, power);
        Field25519.Square(power, power);
        Field25519.Square(valueToTheFourth, value);
        Field25519.Square(valueToTheFourth, valueToTheFourth);
        Field25519.Multiply(power, power, valueToTheFourth);

        Assert.AreEqual(One, Encoded(power));
    }

    [TestMethod]
    public void AreEqual_PAndZero_AreEqual() =>
        Assert.IsTrue(Field25519.AreEqual(Decoded(Prime), Decoded(Zero)));

    [TestMethod]
    public void AreEqual_OneAndTwo_AreNotEqual() =>
        Assert.IsFalse(Field25519.AreEqual(Decoded(One), Decoded("0200000000000000000000000000000000000000000000000000000000000000")));

    [TestMethod]
    [DataRow(One, 1u, DisplayName = "1 is odd")]
    [DataRow(PrimeMinusOne, 0u, DisplayName = "p - 1 is even")]
    [DataRow(Prime, 0u, DisplayName = "p reduces to 0, even")]
    public void Parity_Value_IsTheLowestBitOfTheCanonicalValue(string encoded, uint expected) =>
        Assert.AreEqual(expected, Field25519.Parity(Decoded(encoded)));

    [TestMethod]
    [DataRow(0u, One, PrimeMinusOne, DisplayName = "bit 0 keeps both")]
    [DataRow(1u, PrimeMinusOne, One, DisplayName = "bit 1 swaps")]
    [DataRow(2u, One, PrimeMinusOne, DisplayName = "only the lowest bit is read")]
    public void ConditionalSwap_Bit_SwapsOnlyWhenTheLowestBitIsSet(uint bit, string expectedLeft, string expectedRight)
    {
        Span<long> left = Decoded(One);
        Span<long> right = Decoded(PrimeMinusOne);

        Field25519.ConditionalSwap(left, right, bit);

        Assert.AreEqual(expectedLeft, Encoded(left));
        Assert.AreEqual(expectedRight, Encoded(right));
    }

    [TestMethod]
    [DataRow(0u, One, DisplayName = "bit 0 selects whenZero")]
    [DataRow(1u, PrimeMinusOne, DisplayName = "bit 1 selects whenOne")]
    [DataRow(3u, PrimeMinusOne, DisplayName = "only the lowest bit is read")]
    public void ConditionalSelect_Bit_SelectsByTheLowestBit(uint bit, string expected)
    {
        Span<long> result = stackalloc long[Field25519.LimbCount];

        Field25519.ConditionalSelect(result, Decoded(One), Decoded(PrimeMinusOne), bit);

        Assert.AreEqual(expected, Encoded(result));
    }

    [TestMethod]
    public void SetSmall_Value_EncodesAsThatValue()
    {
        Span<long> element = Decoded(PrimeMinusOne);

        Field25519.SetSmall(element, 0x1234);

        Assert.AreEqual("3412000000000000000000000000000000000000000000000000000000000000", Encoded(element));
    }

    [TestMethod]
    public void Clear_Element_ZeroesEveryLimb()
    {
        long[] element = Decoded(PrimeMinusOne);

        Field25519.Clear(element);

        CollectionAssert.AreEqual(new long[Field25519.LimbCount], element);
    }

    private static long[] Decoded(string encoded)
    {
        long[] element = new long[Field25519.LimbCount];
        _ = Field25519.Decode(element, Convert.FromHexString(encoded));
        return element;
    }

    private static string Encoded(ReadOnlySpan<long> element)
    {
        byte[] encoded = new byte[Field25519.EncodedLength];
        Field25519.Encode(encoded, element);
        return Convert.ToHexStringLower(encoded);
    }
}
