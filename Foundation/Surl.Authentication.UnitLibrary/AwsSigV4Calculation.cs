using System.Security.Cryptography;
using System.Text;

namespace Surl.Authentication;

/// <summary>
/// AWS Signature Version 4's arithmetic, as upstream curl 8.21.0's <c>--aws-sigv4</c> does it
/// (ADR-0043): the string to sign over the canonical request's SHA-256, and the signature, an
/// HMAC-SHA256 under a key derived from the secret through the scope's date, region, service
/// and terminator in turn. The request's text is hashed as the bytes received, one byte per
/// character (Latin-1), as the HTTP server reads them.
/// </summary>
internal static class AwsSigV4Calculation
{
    /// <summary>
    /// The signature of <paramref name="canonicalRequest"/>.
    /// </summary>
    /// <param name="authorization">The credentials, for the provider and the scope.</param>
    /// <param name="requestTime">The request's date field as sent, <c>yyyyMMddTHHmmssZ</c>.</param>
    /// <param name="canonicalRequest">The canonical request.</param>
    /// <param name="secret">The secret access key's bytes.</param>
    /// <returns>The signature's 32 bytes.</returns>
    public static byte[] ComputeSignature(
        AwsSigV4Authorization authorization, string requestTime, string canonicalRequest, byte[] secret)
    {
        var stringToSign = string.Join(
            '\n',
            AwsSigV4Provider.AlgorithmOf(authorization.Provider),
            requestTime,
            authorization.Scope,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.Latin1.GetBytes(canonicalRequest))));

        return HMACSHA256.HashData(DeriveSigningKey(authorization, secret), Encoding.Latin1.GetBytes(stringToSign));
    }

    /// <summary>
    /// The signing key: an HMAC-SHA256 chain from the provider's prefix and the secret through the
    /// scope's date, region, service and terminator in turn.
    /// </summary>
    /// <param name="authorization">The credentials, for the provider and the scope.</param>
    /// <param name="secret">The secret access key's bytes.</param>
    /// <returns>The signing key's 32 bytes.</returns>
    public static byte[] DeriveSigningKey(AwsSigV4Authorization authorization, byte[] secret)
    {
        byte[] key = [.. Encoding.ASCII.GetBytes(AwsSigV4Provider.SecretPrefixOf(authorization.Provider)), .. secret];
        foreach (var scopePart in authorization.Scope.Split('/'))
        {
            key = HMACSHA256.HashData(key, Encoding.Latin1.GetBytes(scopePart));
        }

        return key;
    }

    /// <summary>
    /// The signature of one <c>aws-chunked</c> chunk ("Transferring Payload in Multiple Chunks"):
    /// over <c>&lt;ALGORITHM&gt;-PAYLOAD</c>, the request time, the scope, the previous signature,
    /// the empty string's SHA-256 and the chunk data's SHA-256, one per line.
    /// </summary>
    /// <param name="authorization">The credentials, for the provider and the scope.</param>
    /// <param name="requestTime">The request's date field as sent.</param>
    /// <param name="signingKey">The key <see cref="DeriveSigningKey"/> gave.</param>
    /// <param name="previousSignature">The seed signature for the first chunk, else the chunk before's.</param>
    /// <param name="chunkSha256">The SHA-256 of the chunk's data.</param>
    /// <returns>The chunk's signature's 32 bytes.</returns>
    public static byte[] ComputeChunkSignature(
        AwsSigV4Authorization authorization, string requestTime, byte[] signingKey, byte[] previousSignature, ReadOnlySpan<byte> chunkSha256)
    {
        var stringToSign = string.Join(
            '\n',
            AwsSigV4Provider.AlgorithmOf(authorization.Provider) + "-PAYLOAD",
            requestTime,
            authorization.Scope,
            Convert.ToHexStringLower(previousSignature),
            Convert.ToHexStringLower(SHA256.HashData([])),
            Convert.ToHexStringLower(chunkSha256));

        return HMACSHA256.HashData(signingKey, Encoding.Latin1.GetBytes(stringToSign));
    }
}
