using System.Numerics;

namespace Surl.Cryptography.Sm2;

[TestClass]
public sealed class CurveSm2ParametersTests
{
    // GM/T 0003.5-2012 section 2, OSCCA's recommended curve parameters and RFC 8998 section
    // 3.2.1 all give a = p - 3, the property the doubling formulas rely on.
    [TestMethod]
    public void A_IsPMinusThree() =>
        Assert.AreEqual(CurveSm2Parameters.P - 3, CurveSm2Parameters.A);

    [TestMethod]
    public void G_IsOnTheCurve() =>
        Assert.IsTrue(Sm2PointArithmetic.IsOnCurve(CurveSm2Parameters.G));

    // Cofactor 1 (GM/T 0003.5-2012 Annex B.2): n lies in Hasse's interval around p + 1.
    [TestMethod]
    public void N_LiesWithinHassesBoundOfPPlusOne()
    {
        var difference = BigInteger.Abs(CurveSm2Parameters.P + 1 - CurveSm2Parameters.N);

        Assert.IsLessThanOrEqualTo(4 * CurveSm2Parameters.P, difference * difference);
    }

    [TestMethod]
    public void Parse_LeadingDigitAboveSeven_IsNonNegative() =>
        Assert.AreEqual(new BigInteger(0xFF), CurveSm2Parameters.Parse("FF"));
}
