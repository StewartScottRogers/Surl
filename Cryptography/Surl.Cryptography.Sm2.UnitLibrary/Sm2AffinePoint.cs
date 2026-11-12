using System.Numerics;

namespace Surl.Cryptography.Sm2;

/// <summary>A finite point (x, y) on curveSM2 in affine coordinates, each reduced below p.</summary>
/// <param name="X">The x-coordinate.</param>
/// <param name="Y">The y-coordinate.</param>
internal readonly record struct Sm2AffinePoint(BigInteger X, BigInteger Y);
