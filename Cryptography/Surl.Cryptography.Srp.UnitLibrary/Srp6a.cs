using System.Numerics;
using System.Security.Cryptography;

namespace Surl.Cryptography.Srp;

/// <summary>
/// The SRP-6a formulas of RFC 5054 sections 2.4 to 2.6, as the server uses them: the private key
/// <c>x</c>, the verifier <c>v</c>, the multiplier <c>k</c>, the server's public value <c>B</c>, the
/// scrambler <c>u</c>, the check on the client's <c>A</c> and the premaster secret. Numbers cross the
/// API as unsigned big-endian bytes, the way TLS carries them.
/// </summary>
public static class Srp6a
{
    /// <summary>
    /// <c>PAD(value)</c>: the value as unsigned big-endian bytes, left-padded with zeros to the length
    /// of the group's <c>N</c> (RFC 5054 section 2.6).
    /// </summary>
    /// <param name="group">The group whose <c>N</c> sets the width.</param>
    /// <param name="value">A non-negative value no wider than <c>N</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or wider than <c>N</c>.</exception>
    public static byte[] Pad(SrpGroup group, BigInteger value)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        var length = value.GetByteCount(isUnsigned: true);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, group.PrimeLength, nameof(value));
        var padded = new byte[group.PrimeLength];
        value.TryWriteBytes(padded.AsSpan(group.PrimeLength - length), out _, isUnsigned: true, isBigEndian: true);
        return padded;
    }

    /// <summary>
    /// The value as unsigned big-endian bytes with no leading zero byte (zero is one zero byte), the
    /// form OpenSSL's and GnuTLS's <c>BN_bn2bin</c>-style encoders give.
    /// </summary>
    /// <param name="value">A non-negative value.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public static byte[] ToUnsignedBigEndian(BigInteger value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        return value.ToByteArray(isUnsigned: true, isBigEndian: true);
    }

    /// <summary>Reads unsigned big-endian bytes as a number.</summary>
    /// <param name="bytes">The bytes; empty is zero.</param>
    public static BigInteger FromUnsignedBigEndian(ReadOnlySpan<byte> bytes) =>
        new(bytes, isUnsigned: true, isBigEndian: true);

    /// <summary><c>x = SHA1(s | SHA1(I | ":" | P))</c> (RFC 5054 section 2.6).</summary>
    /// <param name="salt">The salt <c>s</c>.</param>
    /// <param name="userName">The user name <c>I</c>, as the bytes the client's <c>srp</c> extension carries.</param>
    /// <param name="password">The password <c>P</c>, as the bytes the client hashes (UTF-8 in Surl, ADR-0122 decision 4).</param>
    public static BigInteger ComputePrivateKey(ReadOnlySpan<byte> salt, ReadOnlySpan<byte> userName, ReadOnlySpan<byte> password)
    {
        var identity = new byte[userName.Length + 1 + password.Length];
        userName.CopyTo(identity);
        identity[userName.Length] = (byte)':';
        password.CopyTo(identity.AsSpan(userName.Length + 1));
        var inner = SHA1.HashData(identity);
        CryptographicOperations.ZeroMemory(identity);
        return FromUnsignedBigEndian(SHA1.HashData([.. salt, .. inner]));
    }

    /// <summary><c>v = g^x % N</c> with <c>x</c> from <see cref="ComputePrivateKey" /> (RFC 5054 section 2.4).</summary>
    /// <param name="group">The group.</param>
    /// <param name="salt">The salt <c>s</c>.</param>
    /// <param name="userName">The user name <c>I</c>.</param>
    /// <param name="password">The password <c>P</c>.</param>
    public static BigInteger ComputeVerifier(SrpGroup group, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> userName, ReadOnlySpan<byte> password)
    {
        ArgumentNullException.ThrowIfNull(group);
        return BigInteger.ModPow(group.Generator, ComputePrivateKey(salt, userName, password), group.Prime);
    }

    /// <summary><c>k = SHA1(N | PAD(g))</c> (RFC 5054 section 2.5.3).</summary>
    /// <param name="group">The group.</param>
    public static BigInteger ComputeMultiplier(SrpGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return HashToNumber([.. ToUnsignedBigEndian(group.Prime), .. Pad(group, group.Generator)]);
    }

    /// <summary><c>B = (k*v + g^b) % N</c> (RFC 5054 section 2.5.3).</summary>
    /// <param name="group">The group.</param>
    /// <param name="verifier">The user's verifier <c>v</c>.</param>
    /// <param name="serverPrivateValue">The server's secret <c>b</c>.</param>
    public static BigInteger ComputeServerPublicValue(SrpGroup group, BigInteger verifier, BigInteger serverPrivateValue)
    {
        ArgumentNullException.ThrowIfNull(group);
        var generatorPower = BigInteger.ModPow(group.Generator, serverPrivateValue, group.Prime);
        return ((ComputeMultiplier(group) * verifier) + generatorPower) % group.Prime;
    }

    /// <summary>
    /// The client's public value <c>A = g^a % N</c> (RFC 5054 section 2.5.4), for a test that plays
    /// the client against the server.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="clientPrivateValue">The client's secret <c>a</c>.</param>
    public static BigInteger ComputeClientPublicValue(SrpGroup group, BigInteger clientPrivateValue)
    {
        ArgumentNullException.ThrowIfNull(group);
        return BigInteger.ModPow(group.Generator, clientPrivateValue, group.Prime);
    }

    /// <summary><c>u = SHA1(PAD(A) | PAD(B))</c> (RFC 5054 section 2.6).</summary>
    /// <param name="group">The group.</param>
    /// <param name="clientPublicValue">The client's <c>A</c>.</param>
    /// <param name="serverPublicValue">The server's <c>B</c>.</param>
    public static BigInteger ComputeScrambler(SrpGroup group, BigInteger clientPublicValue, BigInteger serverPublicValue) =>
        HashToNumber([.. Pad(group, clientPublicValue), .. Pad(group, serverPublicValue)]);

    /// <summary>
    /// Whether the client's <c>A</c> may be used: the server must abort with <c>illegal_parameter</c>
    /// when <c>A % N = 0</c> (RFC 5054 section 2.5.4).
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="clientPublicValue">The client's <c>A</c>.</param>
    public static bool IsClientPublicValueAcceptable(SrpGroup group, BigInteger clientPublicValue)
    {
        ArgumentNullException.ThrowIfNull(group);
        return !(clientPublicValue % group.Prime).IsZero;
    }

    /// <summary>
    /// The server's premaster secret <c>(A * v^u) ^ b % N</c> (RFC 5054 section 2.6). The caller has
    /// checked <see cref="IsClientPublicValueAcceptable" /> first.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="clientPublicValue">The client's <c>A</c>.</param>
    /// <param name="verifier">The user's verifier <c>v</c>.</param>
    /// <param name="scrambler">The scrambler <c>u</c>.</param>
    /// <param name="serverPrivateValue">The server's secret <c>b</c>.</param>
    public static BigInteger ComputePremasterSecret(
        SrpGroup group, BigInteger clientPublicValue, BigInteger verifier, BigInteger scrambler, BigInteger serverPrivateValue)
    {
        ArgumentNullException.ThrowIfNull(group);
        var verifierPower = BigInteger.ModPow(verifier, scrambler, group.Prime);
        var baseValue = clientPublicValue * verifierPower % group.Prime;
        return BigInteger.ModPow(baseValue, serverPrivateValue, group.Prime);
    }

    private static BigInteger HashToNumber(byte[] message) => FromUnsignedBigEndian(SHA1.HashData(message));
}
