using System.Numerics;

namespace Surl.Cryptography.Sm2;

/// <summary>
/// A point on curveSM2 in Jacobian coordinates: (X, Y, Z) stands for the affine point
/// (X / Z^2, Y / Z^3), and any Z of zero stands for the point at infinity.
/// </summary>
/// <param name="X">The Jacobian X.</param>
/// <param name="Y">The Jacobian Y.</param>
/// <param name="Z">The Jacobian Z; zero for the point at infinity.</param>
internal readonly record struct Sm2JacobianPoint(BigInteger X, BigInteger Y, BigInteger Z)
{
    /// <summary>The point at infinity, the group's identity.</summary>
    public static Sm2JacobianPoint Infinity { get; } = new(BigInteger.One, BigInteger.One, BigInteger.Zero);

    /// <summary>Gets whether this is the point at infinity.</summary>
    public bool IsInfinity => Z.IsZero;

    /// <summary>Lifts a finite affine point to Jacobian coordinates with Z = 1.</summary>
    /// <param name="point">The affine point.</param>
    /// <returns>The same point in Jacobian coordinates.</returns>
    public static Sm2JacobianPoint FromAffine(Sm2AffinePoint point) => new(point.X, point.Y, BigInteger.One);
}
