using System.Security.Cryptography;
using System.Text;

namespace Surl.Authentication;

/// <summary>
/// RFC 2831 section 2.1.2.1's <c>response-value</c> and section 2.1.3's <c>rspauth</c>, for
/// <c>qop=auth</c>. Every directive is hashed as received, one byte per character, and the
/// password as its UTF-8 bytes: upstream curl hashes the bytes it sends and does not convert a
/// UTF-8 name to ISO-8859-1 (ADR-0049, section 5).
/// </summary>
internal static class DigestMd5Calculation
{
    /// <summary>
    /// The <c>response</c> the client sends: <c>A2</c> is <c>AUTHENTICATE:</c> and the
    /// <c>digest-uri</c>.
    /// </summary>
    /// <param name="response">The directives the client sent.</param>
    /// <param name="password">The password's bytes.</param>
    /// <returns>The 16-byte digest.</returns>
    public static byte[] ComputeResponse(DigestMd5Response response, ReadOnlySpan<byte> password) =>
        Compute(response, password, "AUTHENTICATE");

    /// <summary>
    /// The <c>rspauth</c> the server answers with: <c>A2</c> is <c>:</c> and the <c>digest-uri</c>.
    /// </summary>
    /// <param name="response">The directives the client sent.</param>
    /// <param name="password">The password's bytes.</param>
    /// <returns>The 16-byte digest.</returns>
    public static byte[] ComputeResponseAuth(DigestMd5Response response, ReadOnlySpan<byte> password) =>
        Compute(response, password, string.Empty);

    private static byte[] Compute(DigestMd5Response response, ReadOnlySpan<byte> password, string a2Method)
    {
        byte[] userSecret = [.. Encoding.Latin1.GetBytes($"{response.UserName}:{response.Realm}:"), .. password];
        var authorizationId = response.AuthorizationId is null ? string.Empty : ":" + response.AuthorizationId;
        byte[] a1 =
        [
            .. MD5.HashData(userSecret),
            .. Encoding.Latin1.GetBytes($":{response.Nonce}:{response.ClientNonce}{authorizationId}"),
        ];
        var a2 = Encoding.Latin1.GetBytes($"{a2Method}:{response.DigestUri}");

        return MD5.HashData(Encoding.Latin1.GetBytes(
            $"{HashHex(a1)}:{response.Nonce}:{response.NonceCount}:{response.ClientNonce}:{response.Qop}:{HashHex(a2)}"));
    }

    private static string HashHex(byte[] data) => Convert.ToHexStringLower(MD5.HashData(data));
}
