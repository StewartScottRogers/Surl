using System.Globalization;
using System.Numerics;

namespace Surl.Tls13;

/// <summary>
/// A finite field Diffie-Hellman group of RFC 7919 appendix A: a safe prime p with generator 2.
/// The server's exponent is <see cref="ExponentSize"/> random bytes, at least twice the group's
/// security level as RFC 7919 section 5.2 asks.
/// </summary>
/// <param name="Prime">The group's prime p.</param>
/// <param name="ByteLength">The length in bytes of p, and of every key share and shared secret (RFC 8446 sections 4.2.8.1 and 7.4.1).</param>
/// <param name="ExponentSize">The length in bytes of the server's private exponent.</param>
internal sealed record TlsFiniteFieldGroup(BigInteger Prime, int ByteLength, int ExponentSize)
{
    private static readonly BigInteger Generator = 2;

    /// <summary>ffdhe2048, RFC 7919 appendix A.1; a 256-bit exponent covers its 225 bits.</summary>
    private static readonly TlsFiniteFieldGroup Ffdhe2048 = new(
        Hex(
            "FFFFFFFFFFFFFFFFADF85458A2BB4A9AAFDC5620273D3CF1D8B9C583CE2D3695A9E13641146433FBCC939DCE249B3EF9" +
            "7D2FE363630C75D8F681B202AEC4617AD3DF1ED5D5FD65612433F51F5F066ED0856365553DED1AF3B557135E7F57C935" +
            "984F0C70E0E68B77E2A689DAF3EFE8721DF158A136ADE73530ACCA4F483A797ABC0AB182B324FB61D108A94BB2C8E3FB" +
            "B96ADAB760D7F4681D4F42A3DE394DF4AE56EDE76372BB190B07A7C8EE0A6D709E02FCE1CDF7E2ECC03404CD28342F61" +
            "9172FE9CE98583FF8E4F1232EEF28183C3FE3B1B4C6FAD733BB5FCBC2EC22005C58EF1837D1683B2C6F34A26C1B2EFFA" +
            "886B423861285C97FFFFFFFFFFFFFFFF"),
        256,
        32);

    /// <summary>ffdhe3072, RFC 7919 appendix A.2; a 320-bit exponent covers its 275 bits.</summary>
    private static readonly TlsFiniteFieldGroup Ffdhe3072 = new(
        Hex(
            "FFFFFFFFFFFFFFFFADF85458A2BB4A9AAFDC5620273D3CF1D8B9C583CE2D3695A9E13641146433FBCC939DCE249B3EF9" +
            "7D2FE363630C75D8F681B202AEC4617AD3DF1ED5D5FD65612433F51F5F066ED0856365553DED1AF3B557135E7F57C935" +
            "984F0C70E0E68B77E2A689DAF3EFE8721DF158A136ADE73530ACCA4F483A797ABC0AB182B324FB61D108A94BB2C8E3FB" +
            "B96ADAB760D7F4681D4F42A3DE394DF4AE56EDE76372BB190B07A7C8EE0A6D709E02FCE1CDF7E2ECC03404CD28342F61" +
            "9172FE9CE98583FF8E4F1232EEF28183C3FE3B1B4C6FAD733BB5FCBC2EC22005C58EF1837D1683B2C6F34A26C1B2EFFA" +
            "886B4238611FCFDCDE355B3B6519035BBC34F4DEF99C023861B46FC9D6E6C9077AD91D2691F7F7EE598CB0FAC186D91C" +
            "AEFE130985139270B4130C93BC437944F4FD4452E2D74DD364F2E21E71F54BFF5CAE82AB9C9DF69EE86D2BC522363A0D" +
            "ABC521979B0DEADA1DBF9A42D5C4484E0ABCD06BFA53DDEF3C1B20EE3FD59D7C25E41D2B66C62E37FFFFFFFFFFFFFFFF"),
        384,
        40);

    /// <summary>The group of <paramref name="group"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The group is not ffdhe2048 or ffdhe3072.</exception>
    public static TlsFiniteFieldGroup Of(TlsNamedGroup group) => group switch
    {
        TlsNamedGroup.Ffdhe2048 => Ffdhe2048,
        TlsNamedGroup.Ffdhe3072 => Ffdhe3072,
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "Not a finite field group the server exchanges keys in."),
    };

    /// <summary>
    /// Whether <paramref name="publicValue"/>, big-endian, is a public value RFC 8446 section
    /// 4.2.8.1 accepts: 1 &lt; Y &lt; p - 1.
    /// </summary>
    public bool IsValidPublicValue(ReadOnlySpan<byte> publicValue)
    {
        var y = new BigInteger(publicValue, isUnsigned: true, isBigEndian: true);
        return y > BigInteger.One && y < Prime - 1;
    }

    /// <summary>g^<paramref name="exponent"/> mod p, big-endian and left-padded to <see cref="ByteLength"/>.</summary>
    public byte[] ComputePublicValue(ReadOnlySpan<byte> exponent) => Power(Generator, exponent);

    /// <summary>
    /// The shared secret <paramref name="peerPublicValue"/>^<paramref name="exponent"/> mod p,
    /// big-endian and left-padded to <see cref="ByteLength"/> (RFC 8446 section 7.4.1).
    /// </summary>
    public byte[] ComputeSharedSecret(ReadOnlySpan<byte> peerPublicValue, ReadOnlySpan<byte> exponent) =>
        Power(new BigInteger(peerPublicValue, isUnsigned: true, isBigEndian: true), exponent);

    private byte[] Power(BigInteger value, ReadOnlySpan<byte> exponent)
    {
        var result = BigInteger.ModPow(value, new BigInteger(exponent, isUnsigned: true, isBigEndian: true), Prime);
        var encoded = new byte[ByteLength];
        result.TryWriteBytes(encoded.AsSpan(ByteLength - result.GetByteCount(isUnsigned: true)), out _, isUnsigned: true, isBigEndian: true);
        return encoded;
    }

    private static BigInteger Hex(string value) => BigInteger.Parse("0" + value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
