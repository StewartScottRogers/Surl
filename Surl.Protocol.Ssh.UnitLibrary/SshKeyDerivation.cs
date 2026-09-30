using System.Numerics;
using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Derives a session's keys from one key exchange (RFC 4253, section 7.2):
/// <c>K1 = HASH(K || H || X || session_id)</c> for the key's letter X, extended by
/// <c>Kn = HASH(K || H || K1 || ... || Kn-1)</c> until the key is long enough, K hashed as an
/// <c>mpint</c>. The letters are <c>A</c> and <c>B</c> for the client-to-server and
/// server-to-client IVs, <c>C</c> and <c>D</c> for the encryption keys, and <c>E</c> and
/// <c>F</c> for the integrity keys. Each cipher and MAC takes the length it needs (BL-161).
/// </summary>
/// <param name="hashAlgorithm">The key exchange method's hash.</param>
/// <param name="sharedSecret">K.</param>
/// <param name="exchangeHash">H of this exchange.</param>
/// <param name="sessionIdentifier">H of the connection's first exchange.</param>
internal sealed class SshKeyDerivation(
    HashAlgorithmName hashAlgorithm,
    BigInteger sharedSecret,
    byte[] exchangeHash,
    byte[] sessionIdentifier)
{
    /// <summary>
    /// The six letters, in RFC 4253 section 7.2's order.
    /// </summary>
    public static IReadOnlyList<char> Letters { get; } = ['A', 'B', 'C', 'D', 'E', 'F'];

    /// <summary>
    /// Derives one key.
    /// </summary>
    /// <param name="letter">Which key: <c>A</c> to <c>F</c>.</param>
    /// <param name="length">Its length in bytes.</param>
    /// <returns>The key.</returns>
    public byte[] DeriveKey(char letter, int length)
    {
        var prefix = new SshWireWriter();
        prefix.WriteMpint(sharedSecret);
        prefix.WriteBytes(exchangeHash);
        var secretAndHash = prefix.ToArray();

        var key = CryptographicOperations.HashData(hashAlgorithm, [.. secretAndHash, (byte)letter, .. sessionIdentifier]);
        while (key.Length < length)
        {
            key = [.. key, .. CryptographicOperations.HashData(hashAlgorithm, [.. secretAndHash, .. key])];
        }

        return key[..length];
    }
}
