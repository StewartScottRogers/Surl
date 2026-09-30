using System.Buffers;
using System.Security.Cryptography;

namespace Surl.Authentication;

/// <summary>
/// Compares an MD5 digest a client sent as 32 hex digits, in either case, with the one the server
/// computed (ADR-0049, section 5): <c>CRAM-MD5</c>, <c>DIGEST-MD5</c> and <c>APOP</c> all send one.
/// </summary>
internal static class Md5HexDigest
{
    /// <summary>
    /// Whether <paramref name="sentHex"/> is 32 hex digits naming <paramref name="expected"/>. The
    /// bytes are compared with <paramref name="comparer"/> even when the digits are malformed, so
    /// a malformed digest costs what a wrong one does (ADR-0032, section 8).
    /// </summary>
    /// <param name="comparer">The fixed-time comparer.</param>
    /// <param name="expected">The digest computed from the account's password.</param>
    /// <param name="sentHex">The digest as sent.</param>
    /// <returns><see langword="true"/> only when they match.</returns>
    public static bool Matches(ISecretComparer comparer, ReadOnlySpan<byte> expected, ReadOnlySpan<char> sentHex)
    {
        var sent = new byte[MD5.HashSizeInBytes];
        var isHex = sentHex.Length == MD5.HashSizeInBytes * 2
            && Convert.FromHexString(sentHex, sent, out _, out _) == OperationStatus.Done;

        return comparer.FixedTimeEquals(expected, sent) & isHex;
    }
}
