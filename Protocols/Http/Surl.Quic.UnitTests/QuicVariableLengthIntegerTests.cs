namespace Surl.Quic;

[TestClass]
public sealed class QuicVariableLengthIntegerTests
{
    [TestMethod]
    [DataRow(0UL, "00")]
    [DataRow(63UL, "3f")]
    [DataRow(64UL, "4040")]
    [DataRow(16383UL, "7fff")]
    [DataRow(16384UL, "80004000")]
    [DataRow(1073741823UL, "bfffffff")]
    [DataRow(1073741824UL, "c000000040000000")]
    [DataRow(4611686018427387903UL, "ffffffffffffffff")]
    public void Encode_EachLengthAtItsBoundaries_WritesTheShortestEncodingAndReadsBack(ulong value, string expectedHex)
    {
        var encoding = QuicVariableLengthInteger.Encode(value);
        var read = QuicVariableLengthInteger.TryRead(encoding, out var decoded, out var length);

        Assert.AreEqual(expectedHex, Convert.ToHexStringLower(encoding));
        Assert.IsTrue(read);
        Assert.AreEqual(value, decoded);
        Assert.AreEqual(encoding.Length, length);
        Assert.AreEqual(encoding.Length, QuicVariableLengthInteger.EncodedLength(value));
        Assert.AreEqual(encoding.Length, QuicVariableLengthInteger.LengthFromFirstByte(encoding[0]));
    }

    [TestMethod]
    [DataRow("c2197c5eff14e88c", 151288809941952652UL)]
    [DataRow("9d7f3e7d", 494878333UL)]
    [DataRow("7bbd", 15293UL)]
    [DataRow("25", 37UL)]
    [DataRow("4025", 37UL)]
    public void TryRead_Rfc9000AppendixA1Samples_ReadsTheirValues(string hex, ulong expected)
    {
        var read = QuicVariableLengthInteger.TryRead(Convert.FromHexString(hex), out var value, out var length);

        Assert.IsTrue(read);
        Assert.AreEqual(expected, value);
        Assert.AreEqual(hex.Length / 2, length);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("40")]
    [DataRow("80ffff")]
    [DataRow("c0ffffffffffff")]
    public void TryRead_TruncatedVarint_ReturnsFalse(string hex)
    {
        var read = QuicVariableLengthInteger.TryRead(Convert.FromHexString(hex), out var value, out var length);

        Assert.IsFalse(read);
        Assert.AreEqual(0UL, value);
        Assert.AreEqual(0, length);
    }

    [TestMethod]
    public void EncodedLength_ValueAbove2To62Minus1_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicVariableLengthInteger.EncodedLength(QuicVariableLengthInteger.MaxValue + 1));
    }

    [TestMethod]
    public void Write_DestinationTooShort_Throws()
    {
        var destination = new byte[1];

        Assert.ThrowsExactly<ArgumentException>(() => QuicVariableLengthInteger.Write(destination, 64));
    }
}
