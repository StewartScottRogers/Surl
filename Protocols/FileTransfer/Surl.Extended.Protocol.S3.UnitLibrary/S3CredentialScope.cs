namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Reads the credential scope of a Signature Version 4 request,
/// <c>&lt;key&gt;/&lt;date&gt;/&lt;region&gt;/&lt;service&gt;/aws4_request</c> ("Elements of an AWS API request
/// signature"), from the <c>Authorization</c> field or a presigned URL's <c>X-Amz-Credential</c>.
/// It reads only what <see cref="S3Login"/> compares with the <c>Host</c> field and checks nothing:
/// a credential it cannot read is left to the authentication session.
/// </summary>
internal static class S3CredentialScope
{
    private const string Algorithm = "AWS4-HMAC-SHA256";
    private const string CredentialParameter = "Credential=";

    /// <summary>
    /// The region a request's credential scope names. A query with <c>X-Amz-Algorithm</c> is a
    /// presigned URL: its <c>X-Amz-Credential</c> is read only when the algorithm is exactly
    /// <c>AWS4-HMAC-SHA256</c> and there is no <c>Authorization</c> field. Otherwise the first
    /// <c>Authorization</c> field's first <c>Credential=</c> is read, when its scheme is
    /// <c>AWS4-HMAC-SHA256</c> in any case. The region is the credential's third <c>/</c>-separated
    /// part from the end, so an access key may hold a <c>/</c>.
    /// </summary>
    /// <param name="target">The request target as the client sent it.</param>
    /// <param name="fields">The request's fields, in the order received.</param>
    /// <returns>
    /// The region as sent, which may be empty; <see langword="null"/> when no credential is read or
    /// it has fewer than five parts.
    /// </returns>
    public static string? RegionOf(string target, IReadOnlyList<KeyValuePair<string, string>> fields) =>
        CredentialOf(target, fields)?.Split('/') is [_, _, .., var region, _, _] ? region : null;

    private static string? CredentialOf(string target, IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        var queryStart = target.IndexOf('?');
        var query = S3QueryParameters.Parse(queryStart < 0 ? string.Empty : target[(queryStart + 1)..]);
        var authorization = S3RequestFields.Value(fields, "Authorization");

        return S3QueryParameters.Value(query, "X-Amz-Algorithm") is { } algorithm
            ? PresignedCredential(query, algorithm, authorization)
            : AuthorizationCredential(authorization);
    }

    // A query signature beside an Authorization field is the session's to refuse.
    private static string? PresignedCredential(IReadOnlyList<KeyValuePair<string, string>> query, string algorithm, string? authorization) =>
        authorization is null && algorithm == Algorithm ? S3QueryParameters.Value(query, "X-Amz-Credential") : null;

    // <scheme> <name>=<value>,..., of which Credential is read.
    private static string? AuthorizationCredential(string? authorization) =>
        authorization?.Split(' ', 2) is [var scheme, var parameters] && scheme.Equals(Algorithm, StringComparison.OrdinalIgnoreCase)
            ? parameters.Split(',', StringSplitOptions.TrimEntries)
                .FirstOrDefault(parameter => parameter.StartsWith(CredentialParameter, StringComparison.Ordinal))?[CredentialParameter.Length..]
            : null;
}
