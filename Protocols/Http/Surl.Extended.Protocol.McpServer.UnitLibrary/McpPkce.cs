using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// Proof Key for Code Exchange with the <c>S256</c> method, the only one the authorization server accepts
/// (RFC 7636 sections 4.1 and 4.6; ADR-0111 decision 6.3).
/// </summary>
internal static class McpPkce
{
    /// <summary>The one <c>code_challenge_method</c> accepted.</summary>
    public const string Method = "S256";

    /// <summary>Whether a <c>code_verifier</c> matches the <c>code_challenge</c> a code was issued with.</summary>
    /// <param name="verifier">The verifier: 43 to 128 unreserved characters (RFC 7636 section 4.1).</param>
    /// <param name="challenge">The challenge: <c>BASE64URL(SHA256(ASCII(verifier)))</c>.</param>
    /// <returns><see langword="true" /> when the verifier is well-formed and its challenge is the one given, compared in constant time.</returns>
    public static bool Verifies(string verifier, string challenge)
    {
        if (verifier.Length is < 43 or > 128 || !verifier.All(IsUnreserved))
        {
            return false;
        }

        byte[] expected = Encoding.ASCII.GetBytes(Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
        return CryptographicOperations.FixedTimeEquals(expected, Encoding.UTF8.GetBytes(challenge));
    }

    private static bool IsUnreserved(char character) => char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_' or '~';
}
