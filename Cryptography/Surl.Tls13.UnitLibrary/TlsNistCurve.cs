using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;

namespace Surl.Tls13;

/// <summary>
/// A NIST prime curve y^2 = x^3 - 3x + b over GF(p) (FIPS 186-5, SP 800-186 section 3.2.1), with
/// the check that a peer's point lies on it. The check is the server's own, so a bad point is
/// <see cref="TlsAlert.IllegalParameter"/> on every platform rather than whichever exception the
/// platform's import throws.
/// </summary>
/// <param name="Curve">The base class library's named curve.</param>
/// <param name="CoordinateLength">The length in bytes of one coordinate.</param>
/// <param name="Prime">The field prime p.</param>
/// <param name="B">The curve coefficient b.</param>
internal sealed record TlsNistCurve(ECCurve Curve, int CoordinateLength, BigInteger Prime, BigInteger B)
{
    private static readonly TlsNistCurve P256 = new(
        ECCurve.NamedCurves.nistP256,
        32,
        Hex("ffffffff00000001000000000000000000000000ffffffffffffffffffffffff"),
        Hex("5ac635d8aa3a93e7b3ebbd55769886bc651d06b0cc53b0f63bce3c3e27d2604b"));

    private static readonly TlsNistCurve P384 = new(
        ECCurve.NamedCurves.nistP384,
        48,
        Hex("fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffeffffffff0000000000000000ffffffff"),
        Hex("b3312fa7e23ee7e4988e056be3f82d19181d9c6efe8141120314088f5013875ac656398d8a2ed19d2a85c8edd3ec2aef"));

    private static readonly TlsNistCurve P521 = new(
        ECCurve.NamedCurves.nistP521,
        66,
        (BigInteger.One << 521) - 1,
        Hex("0051953eb9618e1c9a1f929a21a0b68540eea2da725b99b315f3b8b489918ef109e156193951ec7e937b1652c0bd3bb1bf073573df883d2c34f1ef451fd46b503f00"));

    /// <summary>The curve of <paramref name="group"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The group is not secp256r1, secp384r1 or secp521r1.</exception>
    public static TlsNistCurve Of(TlsNamedGroup group) => group switch
    {
        TlsNamedGroup.Secp256r1 => P256,
        TlsNamedGroup.Secp384r1 => P384,
        TlsNamedGroup.Secp521r1 => P521,
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "Not a NIST curve the server exchanges keys on."),
    };

    /// <summary>Whether (x, y), big-endian, is a point of the curve: both coordinates below p and y^2 = x^3 - 3x + b mod p.</summary>
    public bool Contains(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
    {
        var px = new BigInteger(x, isUnsigned: true, isBigEndian: true);
        var py = new BigInteger(y, isUnsigned: true, isBigEndian: true);
        if (px >= Prime || py >= Prime)
        {
            return false;
        }

        var left = BigInteger.ModPow(py, 2, Prime);
        var right = (((BigInteger.ModPow(px, 3, Prime) - (3 * px) + B) % Prime) + Prime) % Prime;
        return left == right;
    }

    private static BigInteger Hex(string value) => BigInteger.Parse("0" + value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
