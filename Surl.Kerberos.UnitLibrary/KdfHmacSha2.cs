using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Surl.Kerberos;

/// <summary>
/// <c>KDF-HMAC-SHA2</c> of RFC 8009 section 3: the SP 800-108 counter-mode KDF with a single
/// iteration and no context, <c>k-truncate(HMAC(key, 00000001 | label | 00 | k))</c>.
/// </summary>
internal static class KdfHmacSha2
{
    /// <summary>Derives <paramref name="outputLength" /> bytes from <paramref name="key" /> and <paramref name="label" />.</summary>
    /// <param name="hashAlgorithm">SHA-256 or SHA-384.</param>
    /// <param name="key">The base key.</param>
    /// <param name="label">The label: for key derivation, the key usage and the purpose octet.</param>
    /// <param name="outputLength">The length of the result, in bytes; no more than one HMAC output.</param>
    /// <returns>The derived bytes.</returns>
    public static byte[] Derive(HashAlgorithmName hashAlgorithm, ReadOnlySpan<byte> key, ReadOnlySpan<byte> label, int outputLength)
    {
        byte[] message = new byte[4 + label.Length + 1 + 4];
        BinaryPrimitives.WriteUInt32BigEndian(message, 1);
        label.CopyTo(message.AsSpan(4));
        BinaryPrimitives.WriteUInt32BigEndian(message.AsSpan(4 + label.Length + 1), (uint)(outputLength * 8));
        return CryptographicOperations.HmacData(hashAlgorithm, key, message)[..outputLength];
    }
}
