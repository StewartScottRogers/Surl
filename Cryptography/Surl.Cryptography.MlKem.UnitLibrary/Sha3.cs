namespace Surl.Cryptography.MlKem;

/// <summary>
/// The SHA-3 hash functions and SHAKE extendable-output functions of FIPS 202 section 6 that
/// ML-KEM uses (FIPS 203 section 4.1): SHA3-256 as H, SHA3-512 as G, SHAKE256 as J and PRF, and
/// SHAKE128 as XOF.
/// </summary>
internal static class Sha3
{
    private const byte HashDomain = 0x06;
    private const byte ShakeDomain = 0x1F;

    /// <summary>SHA3-256 of <paramref name="message"/>: 32 bytes.</summary>
    public static byte[] Sha3_256(ReadOnlySpan<byte> message) => Squeeze(new KeccakSponge(136, HashDomain, message), 32);

    /// <summary>SHA3-512 of <paramref name="message"/>: 64 bytes.</summary>
    public static byte[] Sha3_512(ReadOnlySpan<byte> message) => Squeeze(new KeccakSponge(72, HashDomain, message), 64);

    /// <summary>The first <paramref name="length"/> bytes of SHAKE256 of <paramref name="message"/>.</summary>
    public static byte[] Shake256(ReadOnlySpan<byte> message, int length) => Squeeze(new KeccakSponge(136, ShakeDomain, message), length);

    /// <summary>SHAKE128 of <paramref name="message"/>, to squeeze as far as the caller needs.</summary>
    public static KeccakSponge Shake128(ReadOnlySpan<byte> message) => new(168, ShakeDomain, message);

    private static byte[] Squeeze(KeccakSponge sponge, int length)
    {
        var output = new byte[length];
        sponge.Squeeze(output);
        return output;
    }
}
