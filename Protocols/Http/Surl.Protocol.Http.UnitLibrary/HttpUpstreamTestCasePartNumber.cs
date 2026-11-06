using System.Buffers.Binary;
using Surl.HttpMessage;

namespace Surl.Protocol.Http;

/// <summary>
/// The <c>NUM</c> that picks an upstream test case's <c>&lt;dataNUM&gt;</c> or
/// <c>&lt;connectNUM&gt;</c> for one request, by FILEFORMAT.md's rules at tag <c>curl-8_21_0</c>:
/// the test number in the request line when it is over 10000, its remainder by 10000; then
/// 1000 more for Digest, 1001 for an NTLM type-1 message, 1002 for type 3, one more for Basic
/// once <c>NUM</c> is 1000 or more, and for Negotiate the count of Negotiate requests so far.
/// </summary>
/// <remarks>
/// The test number is the run of digits the request's path ends in - the path without its
/// query or fragment, and without scheme and authority when the target is absolute - or, for
/// <c>CONNECT</c>, the run the host name ends in (<c>test.remote.example.com.1059:443</c>).
/// The credentials judged are the <c>Authorization</c> field's, or the
/// <c>Proxy-Authorization</c> field's when there is none.
/// </remarks>
internal static class HttpUpstreamTestCasePartNumber
{
    private const int NtlmMessageTypeOffset = 8;

    /// <summary>
    /// The <c>NUM</c> for <paramref name="head"/>.
    /// </summary>
    /// <param name="head">The request.</param>
    /// <param name="negotiateRequestCount">The case's Negotiate requests so far; one more when this one is.</param>
    /// <returns>The part number; 0 picks the unnumbered part.</returns>
    public static int Of(HttpRequestHead head, ref int negotiateRequestCount)
    {
        var number = FromTarget(head.Method, head.RequestTarget);
        var credentials = head.GetFieldValues("Authorization").Concat(head.GetFieldValues("Proxy-Authorization")).FirstOrDefault();

        return credentials is null ? number : WithCredentials(number, credentials, ref negotiateRequestCount);
    }

    /// <summary>
    /// The part number the request target alone gives.
    /// </summary>
    /// <param name="method">The request's method.</param>
    /// <param name="target">The request target as sent.</param>
    /// <returns>The test number's remainder by 10000 when it is over 10000; otherwise 0.</returns>
    public static int FromTarget(string method, string target)
    {
        var named = method == "CONNECT" ? HostOf(target) : PathOf(target);
        var digits = named.AsSpan(named.Length - TrailingDigitCount(named)).TrimStart('0');

        // Over 10000 means five digits or more once leading zeros go; 10000 itself leaves 0.
        return digits.Length > 4 ? int.Parse(digits[^4..], provider: null) : 0;
    }

    private static int WithCredentials(int number, string credentials, ref int negotiateRequestCount)
    {
        var words = credentials.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var scheme = words.Length > 0 ? words[0] : string.Empty;
        if (string.Equals(scheme, "Negotiate", StringComparison.OrdinalIgnoreCase))
        {
            return number + ++negotiateRequestCount;
        }

        return SchemeNumbers.TryGetValue(scheme, out var add) ? add(number, words.Length > 1 ? words[1] : string.Empty) : number;
    }

    // The number each scheme but Negotiate makes of the target's number, given the credentials' token.
    private static readonly Dictionary<string, Func<int, string, int>> SchemeNumbers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Digest"] = static (number, _) => number + 1000,
        ["NTLM"] = static (number, token) => number + NtlmAddend(token),
        ["Basic"] = static (number, _) => number >= 1000 ? number + 1 : number,
    };

    // An NTLM message's type is the little-endian 32-bit number after its "NTLMSSP\0" signature.
    private static int NtlmAddend(string token)
    {
        var message = new byte[token.Length];
        if (!Convert.TryFromBase64String(token.Trim(), message, out var length) || length < NtlmMessageTypeOffset + 4)
        {
            return 0;
        }

        return BinaryPrimitives.ReadUInt32LittleEndian(message.AsSpan(NtlmMessageTypeOffset)) switch
        {
            1 => 1001,
            3 => 1002,
            _ => 0,
        };
    }

    private static string HostOf(string target)
    {
        var colon = target.LastIndexOf(':');

        return colon < 0 ? target : target[..colon];
    }

    private static string PathOf(string target)
    {
        var schemeEnd = target.IndexOf("://", StringComparison.Ordinal);
        var path = target;
        if (schemeEnd >= 0)
        {
            var pathStart = target.IndexOf('/', schemeEnd + 3);
            path = pathStart < 0 ? string.Empty : target[pathStart..];
        }

        var end = path.IndexOfAny(['?', '#']);

        return end < 0 ? path : path[..end];
    }

    private static int TrailingDigitCount(string text)
    {
        var count = 0;
        while (count < text.Length && char.IsAsciiDigit(text[text.Length - 1 - count]))
        {
            count++;
        }

        return count;
    }
}
