using System.Buffers;
using System.Buffers.Text;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Reads the DNS query a DNS over HTTPS <c>GET</c> carries in its <c>dns</c> parameter (RFC 8484
/// section 4.1; ADR-0092 decision 5): base64url without padding (RFC 4648 section 5).
/// </summary>
internal static class DnsOverHttpsQueryParameter
{
    /// <summary>The parameter's name.</summary>
    public const string Name = "dns";

    /// <summary>
    /// Returns the message the one <c>dns</c> parameter of <paramref name="query"/> - the target's
    /// part after <c>?</c> - decodes to, or <see langword="null"/> when there is none, more than
    /// one, or its value is not base64url without padding. Other parameters are ignored.
    /// </summary>
    public static byte[]? Decode(string query)
    {
        var values = query.Split('&').Select(parameter => parameter.Split('=', 2)).Where(IsNamedDns).ToArray();
        return values.Length == 1 ? DecodeBase64Url(values[0]) : null;
    }

    private static bool IsNamedDns(string[] parameter) => parameter[0] == Name;

    // A parameter with no '=' has the empty value, which decodes to no message.
    private static byte[]? DecodeBase64Url(string[] parameter)
    {
        var value = parameter.Length == 2 ? parameter[1] : string.Empty;
        if (!value.All(IsBase64UrlCharacter))
        {
            return null;
        }

        var decoded = new byte[Base64Url.GetMaxDecodedLength(value.Length)];
        return Base64Url.DecodeFromChars(value, decoded, out _, out var written) == OperationStatus.Done ? decoded[..written] : null;
    }

    // RFC 4648 section 5's alphabet, so padding, whitespace and percent-encoding are all refused.
    private static bool IsBase64UrlCharacter(char character) => char.IsAsciiLetterOrDigit(character) || character is '-' or '_';
}
