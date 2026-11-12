using System.Numerics;

namespace Surl.Cryptography.Sm2;

[TestClass]
public sealed class Sm2PointArithmeticTests
{
    private static readonly Sm2AffinePoint G = CurveSm2Parameters.G;

    // GM/T 0003.5-2012 Annex B.2, OSCCA's English text
    // (http://www.gmbz.org.cn/upload/2018-07-24/1532401863206085511.pdf): user A's public key PA.
    private static readonly Sm2AffinePoint AnnexBPA = new(
        CurveSm2Parameters.Parse("160E12897DF4EDB61DD812FEB96748FBD3CCF4FFE26AA6F6DB9540AF49C94232"),
        CurveSm2Parameters.Parse("4A7DAD08BB9A459531694BEB20AA489D6649975E1BFCF8C4741B78B4B223007F"));

    // GM/T 0003.5-2012 Annex B.2 step B5: the point [x̄A]RA.
    private static readonly Sm2AffinePoint AnnexBXBarARA = new(
        CurveSm2Parameters.Parse("8D62DAF7DC084E4A85D32214686058545837BDC22D6E9AFE015828A8E1094EC2"),
        CurveSm2Parameters.Parse("564DC0FA639B2967E65F3448CA06627EF3FE67C21561C5BEBB39955229A84760"));

    // GM/T 0003.5-2012 Annex B.2 step B5: the point PA + [x̄A]RA.
    private static readonly Sm2AffinePoint AnnexBSum = new(
        CurveSm2Parameters.Parse("85C40F88CECA80E38172093FC4BA458188E7C58AF81CF2AF454EC43143E55615"),
        CurveSm2Parameters.Parse("8C152CB0A131C958C279DEBECC6AB7396A7BC875FC801BB294C284F47F65F6ED"));

    [TestMethod]
    public void IsOnCurve_BasePoint_ReturnsTrue() =>
        Assert.IsTrue(Sm2PointArithmetic.IsOnCurve(G));

    [TestMethod]
    public void IsOnCurve_PublishedPoints_ReturnTrue()
    {
        Assert.IsTrue(Sm2PointArithmetic.IsOnCurve(AnnexBPA));
        Assert.IsTrue(Sm2PointArithmetic.IsOnCurve(AnnexBXBarARA));
        Assert.IsTrue(Sm2PointArithmetic.IsOnCurve(AnnexBSum));
    }

    [TestMethod]
    public void IsOnCurve_BasePointWithYPlusOne_ReturnsFalse() =>
        Assert.IsFalse(Sm2PointArithmetic.IsOnCurve(G with { Y = G.Y + 1 }));

    // G has order n (cofactor 1): [n]G is the point at infinity, [n - 1]G is -G and [n + 1]G is G.
    [TestMethod]
    public void Multiply_OrderTimesBasePoint_IsThePointAtInfinity()
    {
        var product = Sm2PointArithmetic.Multiply(CurveSm2Parameters.N, G);

        Assert.IsTrue(product.IsInfinity);
        Assert.IsFalse(Sm2PointArithmetic.TryToAffine(product, out var affine));
        Assert.AreEqual(default, affine);
    }

    [TestMethod]
    public void Multiply_OrderMinusOneTimesBasePoint_IsTheNegatedBasePoint()
    {
        Assert.IsTrue(Sm2PointArithmetic.TryToAffine(Sm2PointArithmetic.Multiply(CurveSm2Parameters.N - 1, G), out var affine));

        Assert.AreEqual(new Sm2AffinePoint(G.X, CurveSm2Parameters.P - G.Y), affine);
    }

    [TestMethod]
    public void Multiply_OrderPlusOneTimesBasePoint_IsTheBasePoint()
    {
        Assert.IsTrue(Sm2PointArithmetic.TryToAffine(Sm2PointArithmetic.Multiply(CurveSm2Parameters.N + 1, G), out var affine));

        Assert.AreEqual(G, affine);
    }

    [TestMethod]
    public void Multiply_Zero_IsThePointAtInfinity() =>
        Assert.IsTrue(Sm2PointArithmetic.Multiply(BigInteger.Zero, G).IsInfinity);

    // GM/T 0003.5-2012 Annex B.2 step B5 prints [x̄A]RA and then PA + [x̄A]RA.
    [TestMethod]
    public void Add_GmT00035PAAndXBarARA_ReturnsThePublishedSum()
    {
        var sum = Sm2PointArithmetic.Add(Sm2JacobianPoint.FromAffine(AnnexBPA), Sm2JacobianPoint.FromAffine(AnnexBXBarARA));

        Assert.IsTrue(Sm2PointArithmetic.TryToAffine(sum, out var affine));
        Assert.AreEqual(AnnexBSum, affine);
    }

    [TestMethod]
    public void Add_PointToItself_EqualsDoubling()
    {
        var g = Sm2JacobianPoint.FromAffine(G);

        Assert.IsTrue(Sm2PointArithmetic.TryToAffine(Sm2PointArithmetic.Add(g, g), out var added));
        Assert.IsTrue(Sm2PointArithmetic.TryToAffine(Sm2PointArithmetic.Double(g), out var doubled));
        Assert.AreEqual(doubled, added);
        Assert.IsTrue(Sm2PointArithmetic.IsOnCurve(added));
    }

    [TestMethod]
    public void Add_PointAndItsNegation_IsThePointAtInfinity()
    {
        var negated = Sm2JacobianPoint.FromAffine(new Sm2AffinePoint(G.X, CurveSm2Parameters.P - G.Y));

        Assert.IsTrue(Sm2PointArithmetic.Add(Sm2JacobianPoint.FromAffine(G), negated).IsInfinity);
    }

    [TestMethod]
    public void Add_InfinityOnEitherSide_ReturnsTheOtherPoint()
    {
        var g = Sm2JacobianPoint.FromAffine(G);

        Assert.AreEqual(g, Sm2PointArithmetic.Add(Sm2JacobianPoint.Infinity, g));
        Assert.AreEqual(g, Sm2PointArithmetic.Add(g, Sm2JacobianPoint.Infinity));
    }

    [TestMethod]
    public void Double_PointAtInfinity_IsThePointAtInfinity() =>
        Assert.IsTrue(Sm2PointArithmetic.Double(Sm2JacobianPoint.Infinity).IsInfinity);

    [TestMethod]
    public void Reduce_NegativeValue_ReturnsTheResidueBelowP()
    {
        Assert.AreEqual(CurveSm2Parameters.P - 1, Sm2PointArithmetic.Reduce(BigInteger.MinusOne));
        Assert.AreEqual(BigInteger.One, Sm2PointArithmetic.Reduce(CurveSm2Parameters.P + 1));
    }
}
