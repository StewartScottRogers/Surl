using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Authentication;

/// <summary>
/// The string a Shared Key or Shared Key Lite signature is made over, for the Blob and Data Lake
/// services at version 2009-09-19 and later ("Authorize with Shared Key").
/// </summary>
internal static partial class AzureSharedKeyStringToSign
{
    // The twelve standard fields of Shared Key's string, after the verb, in the page's order.
    private static readonly string[] SharedKeyFields =
    [
        "Content-Encoding", "Content-Language", "Content-Length", "Content-MD5", "Content-Type", "Date",
        "If-Modified-Since", "If-Match", "If-None-Match", "If-Unmodified-Since", "Range",
    ];

    // Shared Key Lite's standard fields, after the verb.
    private static readonly string[] SharedKeyLiteFields = ["Content-MD5", "Content-Type", "Date"];

    /// <summary>
    /// Builds the string to sign.
    /// </summary>
    /// <param name="isLite"><see langword="true"/> for Shared Key Lite's shorter form.</param>
    /// <param name="accountName">The account the <c>Authorization</c> field names.</param>
    /// <param name="method">The request method.</param>
    /// <param name="target">The request target.</param>
    /// <param name="fields">The request's header fields.</param>
    /// <returns>The string to sign.</returns>
    public static string Build(
        bool isLite, string accountName, string method, AzureStorageTarget target, IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        var text = new StringBuilder(method).Append('\n');
        foreach (var name in isLite ? SharedKeyLiteFields : SharedKeyFields)
        {
            text.Append(StandardFieldValue(name, fields)).Append('\n');
        }

        AppendCanonicalizedHeaders(text, fields);
        text.Append('/').Append(accountName).Append(target.RawPath);

        return isLite ? AppendLiteResource(text, target) : AppendResourceParameters(text, target);
    }

    // The field's first value; Content-Length is empty when zero (version 2015-02-21 and later).
    private static string StandardFieldValue(string name, IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        var value = fields.FirstOrDefault(field => string.Equals(field.Key, name, StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;

        return name == "Content-Length" && value == "0" ? string.Empty : value;
    }

    // Every x-ms- field, its name lower-cased, its values joined by commas, each value's runs of
    // white space made one space and its ends trimmed, sorted by name, each line ending "\n".
    private static void AppendCanonicalizedHeaders(StringBuilder text, IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        var headers = fields
            .Where(field => field.Key.StartsWith("x-ms-", StringComparison.OrdinalIgnoreCase))
            .GroupBy(field => field.Key.ToLowerInvariant())
            .OrderBy(group => group.Key, StringComparer.Ordinal);
        foreach (var header in headers)
        {
            var values = header.Select(field => WhiteSpaceRun().Replace(field.Value.Trim(), " "));
            text.Append(header.Key).Append(':').Append(string.Join(',', values)).Append('\n');
        }
    }

    // Each query parameter, its name lower-cased, sorted by name, its values sorted and joined by
    // commas, on its own line.
    private static string AppendResourceParameters(StringBuilder text, AzureStorageTarget target)
    {
        var parameters = target.Parameters
            .GroupBy(parameter => parameter.Key.ToLowerInvariant())
            .OrderBy(group => group.Key, StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            var values = parameter.Select(value => value.Value).Order(StringComparer.Ordinal);
            text.Append('\n').Append(parameter.Key).Append(':').Append(string.Join(',', values));
        }

        return text.ToString();
    }

    // Shared Key Lite names only the comp parameter, as "?comp=<value>".
    private static string AppendLiteResource(StringBuilder text, AzureStorageTarget target)
    {
        var comp = target.Parameters.FirstOrDefault(parameter => string.Equals(parameter.Key, "comp", StringComparison.OrdinalIgnoreCase)).Value;

        return comp is null ? text.ToString() : text.Append("?comp=").Append(comp).ToString();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpaceRun();
}
