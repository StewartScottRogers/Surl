namespace Surl.Cryptography.Curve448;

/// <summary>
/// Field448's reduction and inversion, checked against p = 2^448 - 2^224 - 1 as RFC 7748
/// section 4.2 defines it. Little-endian, p is bytes 0 to 27 of 0xFF, byte 28 of 0xFE and
/// bytes 29 to 55 of 0xFF.
/// </summary>
[TestClass]
public sealed class Field448Tests
{
    [TestMethod]
    public void Encode_ThePrimeItself_EncodesZero()
    {
        byte[] encoded = EncodedPrime();
        Span<ulong> element = stackalloc ulong[Field448.LimbCount];
        Field448.Decode(element, encoded);

        Field448.Encode(encoded, element);

        CollectionAssert.AreEqual(new byte[Field448.EncodedLength], encoded);
    }

    [TestMethod]
    public void Encode_ThePrimePlusSeven_EncodesSeven()
    {
        Span<ulong> element = stackalloc ulong[Field448.LimbCount];
        Field448.Decode(element, EncodedPrime());
        element[0] += 7;
        byte[] encoded = new byte[Field448.EncodedLength];
        byte[] expected = new byte[Field448.EncodedLength];
        expected[0] = 7;

        Field448.Encode(encoded, element);

        CollectionAssert.AreEqual(expected, encoded);
    }

    // 2^448 - 1 = p + 2^224.
    [TestMethod]
    public void Encode_TwoToThe448MinusOne_EncodesTwoToThe224()
    {
        byte[] encoded = new byte[Field448.EncodedLength];
        encoded.AsSpan().Fill(0xFF);
        Span<ulong> element = stackalloc ulong[Field448.LimbCount];
        Field448.Decode(element, encoded);
        byte[] expected = new byte[Field448.EncodedLength];
        expected[28] = 1;

        Field448.Encode(encoded, element);

        CollectionAssert.AreEqual(expected, encoded);
    }

    [TestMethod]
    public void Invert_ANonZeroElement_MultipliesBackToOne()
    {
        Span<ulong> value = stackalloc ulong[Field448.LimbCount];
        Span<ulong> inverse = stackalloc ulong[Field448.LimbCount];
        Field448.SetSmall(value, 39081);
        byte[] encoded = new byte[Field448.EncodedLength];
        byte[] one = new byte[Field448.EncodedLength];
        one[0] = 1;

        Field448.Invert(inverse, value);
        Field448.Multiply(inverse, inverse, value);
        Field448.Encode(encoded, inverse);

        CollectionAssert.AreEqual(one, encoded);
    }

    // 2 - 9 = p - 7: p's encoding with 7 taken from its lowest byte.
    [TestMethod]
    public void Subtract_LargerFromSmaller_WrapsModuloPrime()
    {
        Span<ulong> left = stackalloc ulong[Field448.LimbCount];
        Span<ulong> right = stackalloc ulong[Field448.LimbCount];
        Field448.SetSmall(left, 2);
        Field448.SetSmall(right, 9);
        byte[] encoded = new byte[Field448.EncodedLength];
        byte[] expected = EncodedPrime();
        expected[0] = 0xF8;

        Field448.Subtract(left, left, right);
        Field448.Encode(encoded, left);

        CollectionAssert.AreEqual(expected, encoded);
    }

    private static byte[] EncodedPrime()
    {
        byte[] encoded = new byte[Field448.EncodedLength];
        encoded.AsSpan().Fill(0xFF);
        encoded[28] = 0xFE;
        return encoded;
    }
}
