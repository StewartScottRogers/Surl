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

        byte[] key = [.. Encoding.ASCII.GetBytes(AwsSigV4Provider.SecretPrefixOf(authorization.Provider)), .. secret];
        foreach (var scopePart in authorization.Scope.Split('/'))
        {
            key = HMACSHA256.HashData(key, Encoding.Latin1.GetBytes(scopePart));
        }

        return HMACSHA256.HashData(key, Encoding.Latin1.GetBytes(stringToSign));
    }
}
