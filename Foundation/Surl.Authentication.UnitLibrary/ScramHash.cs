using System.Security.Cryptography;

namespace Surl.Authentication;

/// <summary>
/// The hash a SCRAM mechanism is built on (RFC 5802 section 2.2; RFC 7677): <c>H</c>, <c>HMAC</c>
/// and <c>Hi</c>, which is PBKDF2 with HMAC over that hash and one hash's length of output.
/// </summary>
/// <param name="MechanismName">The SASL mechanism's registered name, such as <c>SCRAM-SHA-256</c>.</param>
/// <param name="Algorithm">The hash.</param>
/// <param name="Length">The hash's length in bytes: the length of every key, signature and proof.</param>
internal sealed record ScramHash(string MechanismName, HashAlgorithmName Algorithm, int Length)
{
    /// <summary>
    /// <c>SCRAM-SHA-1</c>, RFC 5802.
    /// </summary>
    public static ScramHash Sha1 { get; } = new("SCRAM-SHA-1", HashAlgorithmName.SHA1, 20);

    /// <summary>
    /// <c>SCRAM-SHA-256</c>, RFC 7677.
    /// </summary>
    public static ScramHash Sha256 { get; } = new("SCRAM-SHA-256", HashAlgorithmName.SHA256, 32);

    /// <summary>
    /// <c>SCRAM-SHA-512</c>, RFC 5802 over SHA-512, as the IANA SASL registry names it (BL-700).
    /// </summary>
    public static ScramHash Sha512 { get; } = new("SCRAM-SHA-512", HashAlgorithmName.SHA512, 64);

    /// <summary>
    /// <c>H(data)</c>.
    /// </summary>
    /// <param name="data">The bytes hashed.</param>
    /// <returns>The hash.</returns>
    public byte[] Hash(ReadOnlySpan<byte> data) => CryptographicOperations.HashData(Algorithm, data);

    /// <summary>
    /// <c>HMAC(key, data)</c>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="data">The bytes authenticated.</param>
    /// <returns>The HMAC.</returns>
    public byte[] Hmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data) =>
        CryptographicOperations.HmacData(Algorithm, key, data);

    /// <summary>
    /// <c>Hi(str, salt, i)</c>: PBKDF2 over <c>HMAC</c>, one hash's length of output.
    /// </summary>
    /// <param name="password">The normalized password's UTF-8 bytes.</param>
    /// <param name="salt">The salt.</param>
    /// <param name="iterationCount">The iteration count.</param>
    /// <returns>The salted password.</returns>
    public byte[] Hi(ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt, int iterationCount) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterationCount, Algorithm, Length);
}
