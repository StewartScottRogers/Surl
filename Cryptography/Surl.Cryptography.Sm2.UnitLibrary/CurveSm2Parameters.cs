using System.Globalization;
using System.Numerics;

namespace Surl.Cryptography.Sm2;

/// <summary>
/// The SM2 recommended curve y^2 = x^3 + ax + b over the 256-bit prime field F(p), with base
/// point G of prime order n and cofactor 1: the curve GB/T 32918.5-2017 defines and RFC 8998
/// section 3.2.1 calls curveSM2 (TLS NamedGroup 41, 0x0029).
/// </summary>
/// <remarks>
/// Every value is copied from, and checked against, three primary sources that agree digit
/// for digit: OSCCA's "SM2椭圆曲线公钥密码算法推荐曲线参数" (the recommended curve parameters,
/// http://www.oscca.gov.cn/sca/xxgk/2010-12/17/1002386/files/b965ce832cc34bc191cb1cde446b860d.pdf),
/// GM/T 0003.5-2012 section 2 in OSCCA's English text
/// (http://www.gmbz.org.cn/upload/2018-07-24/1532401863206085511.pdf), and RFC 8998 section
/// 3.2.1. The cofactor 1 is GM/T 0003.5-2012 Annex B.2's.
/// </remarks>
internal static class CurveSm2Parameters
{
    /// <summary>The field prime p = FFFFFFFE FFFFFFFF FFFFFFFF FFFFFFFF FFFFFFFF 00000000 FFFFFFFF FFFFFFFF.</summary>
    public static readonly BigInteger P = Parse("FFFFFFFEFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF00000000FFFFFFFFFFFFFFFF");

    /// <summary>The coefficient a = FFFFFFFE FFFFFFFF FFFFFFFF FFFFFFFF FFFFFFFF 00000000 FFFFFFFF FFFFFFFC, which is p - 3.</summary>
    public static readonly BigInteger A = Parse("FFFFFFFEFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF00000000FFFFFFFFFFFFFFFC");

    /// <summary>The coefficient b = 28E9FA9E 9D9F5E34 4D5A9E4B CF6509A7 F39789F5 15AB8F92 DDBCBD41 4D940E93.</summary>
    public static readonly BigInteger B = Parse("28E9FA9E9D9F5E344D5A9E4BCF6509A7F39789F515AB8F92DDBCBD414D940E93");

    /// <summary>The order n of G = FFFFFFFE FFFFFFFF FFFFFFFF FFFFFFFF 7203DF6B 21C6052B 53BBF409 39D54123.</summary>
    public static readonly BigInteger N = Parse("FFFFFFFEFFFFFFFFFFFFFFFFFFFFFFFF7203DF6B21C6052B53BBF40939D54123");

    /// <summary>
    /// The base point G: Gx = 32C4AE2C 1F198119 5F990446 6A39C994 8FE30BBF F2660BE1 715A4589 334C74C7,
    /// Gy = BC3736A2 F4F6779C 59BDCEE3 6B692153 D0A9877C C62A4740 02DF32E5 2139F0A0.
    /// </summary>
    public static readonly Sm2AffinePoint G = new(
        Parse("32C4AE2C1F1981195F9904466A39C9948FE30BBFF2660BE1715A4589334C74C7"),
        Parse("BC3736A2F4F6779C59BDCEE36B692153D0A9877CC62A474002DF32E52139F0A0"));

    /// <summary>The length in bytes of a field element or a scalar.</summary>
    public const int ElementSize = 32;

    /// <summary>Parses a big-endian hexadecimal string as a non-negative integer.</summary>
    /// <param name="hexadecimal">The digits, most significant first, with no sign or prefix.</param>
    /// <returns>The integer.</returns>
    internal static BigInteger Parse(string hexadecimal) =>
        BigInteger.Parse("0" + hexadecimal, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
}
