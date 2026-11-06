using System.Numerics;

namespace Surl.Cryptography.Srp;

/// <summary>
/// One SRP group: the safe prime <c>N</c> and the generator <c>g</c> (RFC 5054 section 2.5.3),
/// with the byte length of <c>N</c> that <c>PAD</c> widens every value to. The seven groups of
/// RFC 5054 Appendix A are the static members; a group read from a GnuTLS <c>tpasswd.conf</c>
/// file is constructed directly and may be any other (ADR-0122 decision 4).
/// </summary>
public sealed class SrpGroup
{
    /// <summary>
    /// Creates a group from its prime and generator.
    /// </summary>
    /// <param name="prime">The prime <c>N</c>; greater than one.</param>
    /// <param name="generator">The generator <c>g</c>; greater than one and less than <paramref name="prime" />.</param>
    /// <exception cref="ArgumentOutOfRangeException">Either value is out of its range.</exception>
    public SrpGroup(BigInteger prime, BigInteger generator)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(prime, BigInteger.One);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(generator, BigInteger.One);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(generator, prime);
        Prime = prime;
        Generator = generator;
        PrimeLength = prime.GetByteCount(isUnsigned: true);
    }

    /// <summary>RFC 5054 Appendix A's 1024-bit group, generator 2.</summary>
    public static SrpGroup Rfc5054Group1024 { get; } = FromHex(SrpAppendixAPrimes.Prime1024, 2);

    /// <summary>RFC 5054 Appendix A's 1536-bit group, generator 2.</summary>
    public static SrpGroup Rfc5054Group1536 { get; } = FromHex(SrpAppendixAPrimes.Prime1536, 2);

    /// <summary>RFC 5054 Appendix A's 2048-bit group, generator 2: the group a verifier computed at startup uses (ADR-0122 decision 4).</summary>
    public static SrpGroup Rfc5054Group2048 { get; } = FromHex(SrpAppendixAPrimes.Prime2048, 2);

    /// <summary>RFC 5054 Appendix A's 3072-bit group, generator 5.</summary>
    public static SrpGroup Rfc5054Group3072 { get; } = FromHex(SrpAppendixAPrimes.Prime3072, 5);

    /// <summary>RFC 5054 Appendix A's 4096-bit group, generator 5.</summary>
    public static SrpGroup Rfc5054Group4096 { get; } = FromHex(SrpAppendixAPrimes.Prime4096, 5);

    /// <summary>RFC 5054 Appendix A's 6144-bit group, generator 5.</summary>
    public static SrpGroup Rfc5054Group6144 { get; } = FromHex(SrpAppendixAPrimes.Prime6144, 5);

    /// <summary>RFC 5054 Appendix A's 8192-bit group, generator 19.</summary>
    public static SrpGroup Rfc5054Group8192 { get; } = FromHex(SrpAppendixAPrimes.Prime8192, 19);

    /// <summary>The seven groups of RFC 5054 Appendix A, smallest first.</summary>
    public static IReadOnlyList<SrpGroup> Rfc5054Groups { get; } =
    [
        Rfc5054Group1024,
        Rfc5054Group1536,
        Rfc5054Group2048,
        Rfc5054Group3072,
        Rfc5054Group4096,
        Rfc5054Group6144,
        Rfc5054Group8192,
    ];

    /// <summary>The prime <c>N</c>.</summary>
    public BigInteger Prime { get; }

    /// <summary>The generator <c>g</c>.</summary>
    public BigInteger Generator { get; }

    /// <summary>The length of <c>N</c> in bytes, the width <c>PAD</c> gives a value (RFC 5054 section 2.6).</summary>
    public int PrimeLength { get; }

    /// <summary>
    /// Whether this group is one of RFC 5054 Appendix A's, the only ones OpenSSL's client accepts
    /// (ADR-0122, Context).
    /// </summary>
    public bool IsRfc5054Group => Rfc5054Groups.Any(group => group.Prime == Prime && group.Generator == Generator);

    private static SrpGroup FromHex(string primeHex, int generator) =>
        new(new BigInteger(Convert.FromHexString(primeHex), isUnsigned: true, isBigEndian: true), generator);
}
