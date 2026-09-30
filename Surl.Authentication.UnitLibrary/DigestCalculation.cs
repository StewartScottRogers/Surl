using System.Security.Cryptography;
using System.Text;
using Surl.Cryptography;

namespace Surl.Authentication;

/// <summary>
/// RFC 7616 section 3.4's arithmetic, every value lower-case hex: <c>H(data)</c>, the
/// <c>A1</c> hash of a user, realm and password, its <c>-sess</c> form, and the
/// <c>response</c> for <c>qop=auth</c>.
/// </summary>
internal static class DigestCalculation
{
    /// <summary>
    /// <c>H(data)</c> as lower-case hex.
    /// </summary>
    public static string HashHex(DigestAlgorithm algorithm, ReadOnlySpan<byte> data) =>
        Convert.ToHexStringLower(algorithm switch
        {
            DigestAlgorithm.Md5 => MD5.HashData(data),
            DigestAlgorithm.Sha256 => SHA256.HashData(data),
            _ => Sha512Slash256.HashData(data),
        });

    /// <summary>
    /// <c>H(username ":" realm ":" passwd)</c> (RFC 7616 section 3.4.2), the whole string
    /// encoded with <paramref name="encoding"/>.
    /// </summary>
    public static string ComputeUserHash(
        DigestAlgorithm algorithm, Encoding encoding, string userName, string realm, string password) =>
        HashHex(algorithm, encoding.GetBytes($"{userName}:{realm}:{password}"));

    /// <summary>
    /// The <c>A1</c> hash a response is built on: <paramref name="userHash"/> itself, or for a
    /// <c>-sess</c> algorithm <c>H(userHash ":" nonce ":" cnonce)</c> (RFC 7616 section 3.4.2).
    /// </summary>
    public static string ComputeA1Hash(
        DigestAlgorithmName algorithm, string userHash, string nonce, string cnonce) =>
        algorithm.IsSession
            ? HashHex(algorithm.Algorithm, Encoding.Latin1.GetBytes($"{userHash}:{nonce}:{cnonce}"))
            : userHash;

    /// <summary>
    /// The <c>response</c> for <c>qop=auth</c>:
    /// <c>KD(H(A1), nonce ":" nc ":" cnonce ":" qop ":" H(method ":" uri))</c>
    /// (RFC 7616 section 3.4.1). The strings are hashed as the bytes received (Latin-1).
    /// </summary>
    public static string ComputeResponse(DigestAlgorithm algorithm, string a1Hash, DigestResponseInputs inputs)
    {
        var a2Hash = HashHex(algorithm, Encoding.Latin1.GetBytes($"{inputs.Method}:{inputs.Uri}"));

        return HashHex(
            algorithm,
            Encoding.Latin1.GetBytes($"{a1Hash}:{inputs.Nonce}:{inputs.NonceCount}:{inputs.Cnonce}:{inputs.Qop}:{a2Hash}"));
    }
}
