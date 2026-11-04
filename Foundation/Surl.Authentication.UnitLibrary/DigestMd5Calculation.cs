using System.Security.Cryptography;
using System.Text;

namespace Surl.Authentication;

/// <summary>
/// RFC 2831 section 2.1.2.1's <c>response-value</c>, section 2.1.3's <c>rspauth</c>, and the
/// session key <c>H(A1)</c> the security layer's keys come from (sections 2.3 and 2.4). Every
/// directive is hashed as received, one byte per character, and the password as its UTF-8 bytes:
/// upstream curl hashes the bytes it sends and does not convert a UTF-8 name to ISO-8859-1
/// (ADR-0049, section 5). For <c>qop=auth-int</c> and <c>auth-conf</c>, <c>A2</c> ends in
/// <c>:00000000000000000000000000000000</c>, as section 2.1.2.1 says.
/// </summary>
internal static class DigestMd5Calculation
{
    private const string SecurityLayerA2Suffix = ":00000000000000000000000000000000";

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

    /// <summary>
    /// <c>H(A1)</c>, the MD5 of <c>A1</c>: the session key the integrity and confidentiality keys
    /// are derived from (RFC 2831, sections 2.3 and 2.4).
    /// </summary>
    /// <param name="response">The directives the client sent.</param>
    /// <param name="password">The password's bytes.</param>
    /// <returns>The 16-byte session key.</returns>
    public static byte[] ComputeSessionKey(DigestMd5Response response, ReadOnlySpan<byte> password)
    {
        byte[] userSecret = [.. Encoding.Latin1.GetBytes($"{response.UserName}:{response.Realm}:"), .. password];
        var authorizationId = response.AuthorizationId is null ? string.Empty : ":" + response.AuthorizationId;

        return MD5.HashData(
        [
            .. MD5.HashData(userSecret),
            .. Encoding.Latin1.GetBytes($":{response.Nonce}:{response.ClientNonce}{authorizationId}"),
        ]);
    }

    private static byte[] Compute(DigestMd5Response response, ReadOnlySpan<byte> password, string a2Method)
    {
        var a2Suffix = response.HasSecurityLayer ? SecurityLayerA2Suffix : string.Empty;
        var a2 = Encoding.Latin1.GetBytes($"{a2Method}:{response.DigestUri}{a2Suffix}");
        var sessionKey = Convert.ToHexStringLower(ComputeSessionKey(response, password));

        return MD5.HashData(Encoding.Latin1.GetBytes(
            $"{sessionKey}:{response.Nonce}:{response.NonceCount}:{response.ClientNonce}:{response.Qop}:{HashHex(a2)}"));
    }

    private static string HashHex(byte[] data) => Convert.ToHexStringLower(MD5.HashData(data));
}
