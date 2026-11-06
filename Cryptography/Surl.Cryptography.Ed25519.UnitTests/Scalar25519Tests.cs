using System.Numerics;

namespace Surl.Cryptography.Ed25519;

/// <summary>
/// Pins the arithmetic modulo the Ed25519 group order L in <see cref="Scalar25519" />
/// against <see cref="BigInteger" />.
/// </summary>
[TestClass]
public sealed class Scalar25519Tests
{
    private const string OrderEncoding = "edd3f55c1a631258d69cf7a2def9de1400000000000000000000000000000010";

    private static readonly BigInteger Order = BigInteger.Pow(2, 252) + BigInteger.Parse("27742317777372353535851937790883648493");

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void Reduce_WideValue_MatchesBigIntegerModulo(int seed)
    {
        byte[] wide = new byte[Scalar25519.WideLength];
        new Random(seed).NextBytes(wide);
        byte[] result = new byte[Scalar25519.EncodedLength];

        Scalar25519.Reduce(result, wide);

        Assert.AreEqual(new BigInteger(wide, isUnsigned: true) % Order, new BigInteger(result, isUnsigned: true));
    }

    [TestMethod]
    public void Reduce_AllOnes_MatchesBigIntegerModulo()
    {
        byte[] wide = new byte[Scalar25519.WideLength];
        wide.AsSpan().Fill(0xFF);
        byte[] result = new byte[Scalar25519.EncodedLength];

        Scalar25519.Reduce(result, wide);

        Assert.AreEqual(new BigInteger(wide, isUnsigned: true) % Order, new BigInteger(result, isUnsigned: true));
    }

    [TestMethod]
    [DataRow(4)]
    [DataRow(5)]
    public void MultiplyAdd_Scalars_MatchBigInteger(int seed)
    {
        Random random = new(seed);
        byte[] left = new byte[Scalar25519.EncodedLength];
        byte[] right = new byte[Scalar25519.EncodedLength];
        byte[] addend = new byte[Scalar25519.EncodedLength];
        random.NextBytes(left);
        random.NextBytes(right);
        random.NextBytes(addend);
        byte[] result = new byte[Scalar25519.EncodedLength];

        Scalar25519.MultiplyAdd(result, left, right, addend);

        BigInteger expected = ((new BigInteger(left, isUnsigned: true) * new BigInteger(right, isUnsigned: true))
            + new BigInteger(addend, isUnsigned: true)) % Order;
        Assert.AreEqual(expected, new BigInteger(result, isUnsigned: true));
    }

    [TestMethod]
    [DataRow("0000000000000000000000000000000000000000000000000000000000000000", true)]
    [DataRow("ecd3f55c1a631258d69cf7a2def9de1400000000000000000000000000000010", true)]
    [DataRow(OrderEncoding, false)]
    [DataRow("eed3f55c1a631258d69cf7a2def9de1400000000000000000000000000000010", false)]
    [DataRow("0000000000000000000000000000000000000000000000000000000000000011", false)]
    public void IsBelowOrder_Scalar_ComparesWithTheGroupOrder(string scalar, bool expected)
    {
        Assert.AreEqual(expected, Scalar25519.IsBelowOrder(Convert.FromHexString(scalar)));
    }
}
