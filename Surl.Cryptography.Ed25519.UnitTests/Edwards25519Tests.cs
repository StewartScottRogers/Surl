namespace Surl.Cryptography.Ed25519;

/// <summary>
/// Pins the edwards25519 point arithmetic in <see cref="Edwards25519" /> that Ed25519
/// signs with.
/// </summary>
[TestClass]
public sealed class Edwards25519Tests
{
    // RFC 8032 section 5.1: B has y = 4/5 and a positive (even) x.
    private const string BasePointEncoding = "5866666666666666666666666666666666666666666666666666666666666666";

    [TestMethod]
    public void TryDecode_BasePoint_EncodesBackToItself()
    {
        long[] point = new long[Edwards25519.PointLength];
        byte[] encoded = new byte[Edwards25519.EncodedLength];

        bool decoded = Edwards25519.TryDecode(point, Convert.FromHexString(BasePointEncoding));
        Edwards25519.Encode(encoded, point);

        Assert.IsTrue(decoded);
        Assert.AreEqual(BasePointEncoding, Convert.ToHexStringLower(encoded));
    }

    [TestMethod]
    public void Encode_Neutral_IsYOne()
    {
        long[] point = new long[Edwards25519.PointLength];
        byte[] encoded = new byte[Edwards25519.EncodedLength];

        Edwards25519.SetNeutral(point);
        Edwards25519.Encode(encoded, point);

        Assert.AreEqual("01" + new string('0', 62), Convert.ToHexStringLower(encoded));
    }

    [TestMethod]
    public void Add_BasePointToItself_IsTwiceTheBasePoint()
    {
        long[] sum = new long[Edwards25519.PointLength];
        long[] product = new long[Edwards25519.PointLength];
        byte[] two = new byte[Scalar25519.EncodedLength];
        two[0] = 2;
        byte[] sumEncoding = new byte[Edwards25519.EncodedLength];
        byte[] productEncoding = new byte[Edwards25519.EncodedLength];
        Assert.IsTrue(Edwards25519.TryDecode(sum, Convert.FromHexString(BasePointEncoding)));

        Edwards25519.Add(sum, sum);
        Edwards25519.ScalarMultiplyBase(product, two);
        Edwards25519.Encode(sumEncoding, sum);
        Edwards25519.Encode(productEncoding, product);

        Assert.AreEqual(Convert.ToHexStringLower(productEncoding), Convert.ToHexStringLower(sumEncoding));
    }

    [TestMethod]
    public void Negate_BasePoint_FlipsTheSignBit()
    {
        long[] point = new long[Edwards25519.PointLength];
        byte[] encoded = new byte[Edwards25519.EncodedLength];
        Assert.IsTrue(Edwards25519.TryDecode(point, Convert.FromHexString(BasePointEncoding)));

        Edwards25519.Negate(point);
        Edwards25519.Encode(encoded, point);

        Assert.AreEqual("58666666666666666666666666666666666666666666666666666666666666e6", Convert.ToHexStringLower(encoded));
    }

    // y = 0 gives x^2 = -1, whose root is sqrt(-1): both signs decode.
    [TestMethod]
    [DataRow("0000000000000000000000000000000000000000000000000000000000000000")]
    [DataRow("0000000000000000000000000000000000000000000000000000000000000080")]
    public void TryDecode_YZero_DecodesWithEitherSign(string encoding)
    {
        long[] point = new long[Edwards25519.PointLength];
        byte[] encoded = new byte[Edwards25519.EncodedLength];

        bool decoded = Edwards25519.TryDecode(point, Convert.FromHexString(encoding));
        Edwards25519.Encode(encoded, point);

        Assert.IsTrue(decoded);
        Assert.AreEqual(encoding, Convert.ToHexStringLower(encoded));
    }

    // y = p is y = 0 written non-canonically; y = 1 with the sign bit set asks for a
    // negative zero; y = 2 has no x on the curve.
    [TestMethod]
    [DataRow("edffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f")]
    [DataRow("0100000000000000000000000000000000000000000000000000000000000080")]
    [DataRow("0200000000000000000000000000000000000000000000000000000000000000")]
    public void TryDecode_InvalidEncoding_IsFalse(string encoding)
    {
        long[] point = new long[Edwards25519.PointLength];

        Assert.IsFalse(Edwards25519.TryDecode(point, Convert.FromHexString(encoding)));
    }
}
