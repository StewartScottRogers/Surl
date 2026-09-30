using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// One of the three NIST prime curves SSH names <c>nistp256</c>, <c>nistp384</c> and
/// <c>nistp521</c> (RFC 5656 section 10.1), with the hash its <c>ecdh-sha2-*</c> and
/// <c>ecdsa-sha2-*</c> algorithms use (section 6.2.1), and the check that a point the client
/// sent lies on it (section 3.1, SEC 1 section 3.2.2.1). The check is done here rather than
/// left to the platform's key import, so every platform refuses the same points.
/// </summary>
internal sealed class SshNistCurve
{
    private readonly BigInteger prime;

    private readonly BigInteger coefficientB;

    private SshNistCurve(string identifier, string oid, ECCurve curve, int fieldLength, HashAlgorithmName hashAlgorithm, string primeHex, string coefficientBHex)
    {
        Identifier = identifier;
        Oid = oid;
        Curve = curve;
        FieldLength = fieldLength;
        HashAlgorithm = hashAlgorithm;
        prime = BigInteger.Parse("0" + primeHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        coefficientB = BigInteger.Parse("0" + coefficientBHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    /// <summary>P-256 (<c>secp256r1</c>) with SHA-256.</summary>
    public static SshNistCurve NistP256 { get; } = new(
        "nistp256",
        "1.2.840.10045.3.1.7",
        ECCurve.NamedCurves.nistP256,
        32,
        HashAlgorithmName.SHA256,
        "FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF",
        "5AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B");

    /// <summary>P-384 (<c>secp384r1</c>) with SHA-384.</summary>
    public static SshNistCurve NistP384 { get; } = new(
        "nistp384",
        "1.3.132.0.34",
        ECCurve.NamedCurves.nistP384,
        48,
        HashAlgorithmName.SHA384,
        "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEFFFFFFFF0000000000000000FFFFFFFF",
        "B3312FA7E23EE7E4988E056BE3F82D19181D9C6EFE8141120314088F5013875AC656398D8A2ED19D2A85C8EDD3EC2AEF");

    /// <summary>P-521 (<c>secp521r1</c>) with SHA-512.</summary>
    public static SshNistCurve NistP521 { get; } = new(
        "nistp521",
        "1.3.132.0.35",
        ECCurve.NamedCurves.nistP521,
        66,
        HashAlgorithmName.SHA512,
        "01FF" + new string('F', 128),
        "0051953EB9618E1C9A1F929A21A0B68540EEA2DA725B99B315F3B8B489918EF109E156193951EC7E937B1652C0BD3BB1BF073573DF883D2C34F1EF451FD46B503F00");

    /// <summary>The three curves.</summary>
    public static IReadOnlyList<SshNistCurve> All { get; } = [NistP256, NistP384, NistP521];

    /// <summary>The name SSH gives the curve, as in <c>ecdsa-sha2-nistp256</c>.</summary>
    public string Identifier { get; }

    /// <summary>The curve's ASN.1 object identifier, dotted, as PKCS #8 and SEC 1 key files name it.</summary>
    public string Oid { get; }

    /// <summary>The curve as the BCL names it.</summary>
    public ECCurve Curve { get; }

    /// <summary>The length in bytes of one coordinate.</summary>
    public int FieldLength { get; }

    /// <summary>The hash the curve's SSH algorithms use.</summary>
    public HashAlgorithmName HashAlgorithm { get; }

    /// <summary>
    /// Encodes a point uncompressed, <c>0x04 || X || Y</c>, each coordinate
    /// <see cref="FieldLength"/> bytes (SEC 1 section 2.3.3), as SSH's <c>Q</c> carries it.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>The encoding.</returns>
    public byte[] EncodePoint(ECPoint point)
    {
        var encoded = new byte[1 + (2 * FieldLength)];
        encoded[0] = 0x04;
        point.X.CopyTo(encoded.AsSpan(1 + FieldLength - point.X!.Length));
        point.Y.CopyTo(encoded.AsSpan(1 + (2 * FieldLength) - point.Y!.Length));

        return encoded;
    }

    /// <summary>
    /// Decodes the client's uncompressed point and checks that it lies on the curve.
    /// </summary>
    /// <param name="encoded">The encoding, <c>0x04 || X || Y</c>.</param>
    /// <returns>The public key parameters.</returns>
    /// <exception cref="SshDisconnectRequiredException">
    /// The encoding is not uncompressed or has the wrong length, a coordinate is not below the
    /// prime, or the point is not on the curve: <c>DISCONNECT</c> 2.
    /// </exception>
    public ECParameters DecodePublicPoint(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length != 1 + (2 * FieldLength) || encoded[0] != 0x04)
        {
            throw SshDisconnectRequiredException.ProtocolError(
                $"The client's {Identifier} point is not an uncompressed point of {1 + (2 * FieldLength)} bytes.");
        }

        var x = encoded.Slice(1, FieldLength).ToArray();
        var y = encoded.Slice(1 + FieldLength, FieldLength).ToArray();
        if (!IsOnCurve(ToInteger(x), ToInteger(y)))
        {
            throw SshDisconnectRequiredException.ProtocolError($"The client's {Identifier} point is not on the curve.");
        }

        return new ECParameters { Curve = Curve, Q = new ECPoint { X = x, Y = y } };
    }

    /// <summary>
    /// Whether <paramref name="point"/> lies on this curve, its coordinates
    /// <see cref="FieldLength"/> bytes each.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>Whether it is on the curve.</returns>
    public bool Holds(ECPoint point) =>
        point.X!.Length == FieldLength && IsOnCurve(ToInteger(point.X), ToInteger(point.Y!));

    private static BigInteger ToInteger(byte[] bigEndian) => new(bigEndian, isUnsigned: true, isBigEndian: true);

    // y^2 = x^3 - 3x + b (mod p), with both coordinates below p.
    private bool IsOnCurve(BigInteger x, BigInteger y)
    {
        if (x >= prime || y >= prime)
        {
            return false;
        }

        var left = BigInteger.ModPow(y, 2, prime);
        var right = (((BigInteger.ModPow(x, 3, prime) - (3 * x) + coefficientB) % prime) + prime) % prime;

        return left == right;
    }
}
