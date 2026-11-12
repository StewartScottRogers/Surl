using System.Numerics;

namespace Surl.Cryptography.Sm2;

/// <summary>
/// The group law on curveSM2 over F(p): the curve-membership test, Jacobian doubling and
/// addition, left-to-right double-and-add scalar multiplication, and the conversion back to
/// affine coordinates.
/// </summary>
/// <remarks>
/// Not constant-time: <see cref="BigInteger" /> arithmetic and the scalar's bits choose
/// branches and running times. That is acceptable for the ephemeral keys of a loopback test
/// server and is stated in this library's CLAUDE.md.
/// </remarks>
internal static class Sm2PointArithmetic
{
    private static readonly BigInteger P = CurveSm2Parameters.P;

    /// <summary>Reduces <paramref name="value" /> into [0, p).</summary>
    /// <param name="value">Any integer, negative included.</param>
    /// <returns>The residue in [0, p).</returns>
    public static BigInteger Reduce(BigInteger value)
    {
        var residue = BigInteger.Remainder(value, P);
        return residue.Sign < 0 ? residue + P : residue;
    }

    /// <summary>Whether (x, y), each already below p, satisfies y^2 = x^3 + ax + b mod p.</summary>
    /// <param name="point">The candidate point.</param>
    /// <returns>True when the point is on the curve.</returns>
    public static bool IsOnCurve(Sm2AffinePoint point)
    {
        var left = Reduce(point.Y * point.Y);
        var right = Reduce((((point.X * point.X) + CurveSm2Parameters.A) * point.X) + CurveSm2Parameters.B);
        return left == right;
    }

    /// <summary>
    /// Doubles a point with the a = -3 formulas ("dbl-2001-b", Bernstein and Lange's Explicit-Formulas
    /// Database). The point at infinity doubles to Z3 = 0, the point at infinity, with no branch;
    /// no finite point of curveSM2 has y = 0, because its order n is odd.
    /// </summary>
    /// <param name="point">The point to double.</param>
    /// <returns>2 * <paramref name="point" />.</returns>
    public static Sm2JacobianPoint Double(Sm2JacobianPoint point)
    {
        var delta = Reduce(point.Z * point.Z);
        var gamma = Reduce(point.Y * point.Y);
        var beta = Reduce(point.X * gamma);
        var alpha = Reduce(3 * (point.X - delta) * (point.X + delta));
        var x3 = Reduce((alpha * alpha) - (8 * beta));
        var z3 = Reduce(((point.Y + point.Z) * (point.Y + point.Z)) - gamma - delta);
        var y3 = Reduce((alpha * ((4 * beta) - x3)) - (8 * gamma * gamma));
        return new Sm2JacobianPoint(x3, y3, z3);
    }

    /// <summary>
    /// Adds two points in Jacobian coordinates ("add-1998-cmo-2"), falling back to
    /// <see cref="Double" /> for equal points and returning the point at infinity for a point
    /// and its negation.
    /// </summary>
    /// <param name="first">The first addend.</param>
    /// <param name="second">The second addend.</param>
    /// <returns><paramref name="first" /> + <paramref name="second" />.</returns>
    public static Sm2JacobianPoint Add(Sm2JacobianPoint first, Sm2JacobianPoint second)
    {
        if (first.IsInfinity)
        {
            return second;
        }

        if (second.IsInfinity)
        {
            return first;
        }

        var firstZSquared = Reduce(first.Z * first.Z);
        var secondZSquared = Reduce(second.Z * second.Z);
        var u1 = Reduce(first.X * secondZSquared);
        var u2 = Reduce(second.X * firstZSquared);
        var s1 = Reduce(first.Y * secondZSquared * second.Z);
        var s2 = Reduce(second.Y * firstZSquared * first.Z);
        if (u1 == u2)
        {
            return s1 == s2 ? Double(first) : Sm2JacobianPoint.Infinity;
        }

        var h = Reduce(u2 - u1);
        var r = Reduce(s2 - s1);
        var hSquared = Reduce(h * h);
        var hCubed = Reduce(hSquared * h);
        var u1HSquared = Reduce(u1 * hSquared);
        var x3 = Reduce((r * r) - hCubed - (2 * u1HSquared));
        var y3 = Reduce((r * (u1HSquared - x3)) - (s1 * hCubed));
        var z3 = Reduce(h * first.Z * second.Z);
        return new Sm2JacobianPoint(x3, y3, z3);
    }

    /// <summary>
    /// Computes [<paramref name="scalar" />]<paramref name="point" /> by left-to-right
    /// double-and-add over the scalar's bits.
    /// </summary>
    /// <param name="scalar">A non-negative scalar; zero gives the point at infinity.</param>
    /// <param name="point">A finite point on the curve.</param>
    /// <returns>The product in Jacobian coordinates.</returns>
    public static Sm2JacobianPoint Multiply(BigInteger scalar, Sm2AffinePoint point)
    {
        var addend = Sm2JacobianPoint.FromAffine(point);
        var accumulator = Sm2JacobianPoint.Infinity;
        for (var bit = (int)scalar.GetBitLength() - 1; bit >= 0; bit--)
        {
            accumulator = Double(accumulator);
            if (!(scalar >> bit).IsEven)
            {
                accumulator = Add(accumulator, addend);
            }
        }

        return accumulator;
    }

    /// <summary>Converts a point to affine coordinates, dividing by Z with Fermat's inverse Z^(p - 2).</summary>
    /// <param name="point">The point to convert.</param>
    /// <param name="affine">The affine point; default when the point is at infinity.</param>
    /// <returns>False when <paramref name="point" /> is the point at infinity, which has no affine form.</returns>
    public static bool TryToAffine(Sm2JacobianPoint point, out Sm2AffinePoint affine)
    {
        if (point.IsInfinity)
        {
            affine = default;
            return false;
        }

        var zInverse = BigInteger.ModPow(point.Z, P - 2, P);
        var zInverseSquared = Reduce(zInverse * zInverse);
        affine = new Sm2AffinePoint(
            Reduce(point.X * zInverseSquared),
            Reduce(point.Y * zInverseSquared * zInverse));
        return true;
    }
}
