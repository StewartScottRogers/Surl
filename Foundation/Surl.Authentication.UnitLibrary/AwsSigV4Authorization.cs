namespace Surl.Authentication;

/// <summary>
/// The credentials of an AWS Signature Version 4 <c>Authorization</c> field, read as upstream
/// curl 8.21.0's <c>--aws-sigv4</c> writes them after the <c>&lt;PROVIDER&gt;4-HMAC-SHA256</c>
/// scheme: <c>Credential=&lt;key&gt;/&lt;date&gt;/&lt;region&gt;/&lt;service&gt;/&lt;provider&gt;4_request,
/// SignedHeaders=&lt;name&gt;;..., Signature=&lt;hex&gt;</c> (ADR-0043).
/// </summary>
/// <param name="AccessKeyId">The access key ID: the account's user name.</param>
/// <param name="Scope">The credential scope after the key: <c>date/region/service/terminator</c>.</param>
/// <param name="ScopeDate">The scope's <c>yyyyMMdd</c> date.</param>
/// <param name="Service">The scope's service, which decides how the path is canonicalised.</param>
/// <param name="Provider">The scope terminator's provider, lower case (<c>aws</c> in <c>aws4_request</c>).</param>
/// <param name="SignedHeaders">The signed header names, lower case, in the order sent.</param>
/// <param name="Signature">The signature's 32 bytes.</param>
internal sealed record AwsSigV4Authorization(
    string AccessKeyId,
    string Scope,
    string ScopeDate,
    string Service,
    string Provider,
    IReadOnlyList<string> SignedHeaders,
    byte[] Signature)
{
    private const string ScopeTerminatorSuffix = "4_request";

    /// <summary>
    /// Reads <paramref name="credentials"/>; anything malformed reads as <see langword="null"/>,
    /// never an exception.
    /// </summary>
    /// <param name="credentials">What followed the scheme, as received.</param>
    /// <returns>The credentials, or <see langword="null"/>.</returns>
    public static AwsSigV4Authorization? TryParse(string credentials)
    {
        var parameters = ReadParameters(credentials);

        return parameters?.Count == 3
            && parameters.TryGetValue("Credential", out var credential)
            && parameters.TryGetValue("SignedHeaders", out var signedHeaders)
            && parameters.TryGetValue("Signature", out var signature)
            ? Create(credential, signedHeaders, signature)
            : null;
    }

    // name=value pairs separated by commas and spaces; a pair with no = or a name given twice
    // reads as null.
    private static Dictionary<string, string>? ReadParameters(string credentials)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in credentials.Split(','))
        {
            var pair = part.Trim(' ').Split('=', 2);
            if (pair.Length != 2 || !parameters.TryAdd(pair[0], pair[1]))
            {
                return null;
            }
        }

        return parameters;
    }

    private static AwsSigV4Authorization? Create(string credential, string signedHeaders, string signature)
    {
        // The key is everything before the last four parts, so a key holding a / still reads.
        var parts = credential.Split('/');
        var signatureBytes = ReadSignature(signature);
        if (parts.Length < 5 || signatureBytes is null || signedHeaders.Length == 0)
        {
            return null;
        }

        var terminator = parts[^1];
        var provider = terminator.EndsWith(ScopeTerminatorSuffix, StringComparison.Ordinal)
            ? terminator[..^ScopeTerminatorSuffix.Length]
            : string.Empty;

        return AwsSigV4Provider.IsName(provider)
            ? new AwsSigV4Authorization(
                string.Join('/', parts[..^4]),
                string.Join('/', parts[^4..]),
                parts[^4],
                parts[^2],
                provider,
                signedHeaders.Split(';'),
                signatureBytes)
            : null;
    }

    private static byte[]? ReadSignature(string signature) =>
        signature.Length == 64 && signature.All(char.IsAsciiHexDigitLower) ? Convert.FromHexString(signature) : null;
}
