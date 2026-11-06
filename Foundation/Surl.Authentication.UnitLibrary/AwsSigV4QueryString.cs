using System.Globalization;

namespace Surl.Authentication;

/// <summary>
/// The Signature Version 4 parameters of a presigned URL ("Authenticating Requests: Using Query
/// Parameters (AWS Signature Version 4)"; ADR-0108 decision 4.5): <c>X-Amz-Algorithm</c>,
/// <c>X-Amz-Credential</c>, <c>X-Amz-Date</c>, <c>X-Amz-Expires</c>, <c>X-Amz-SignedHeaders</c> and
/// <c>X-Amz-Signature</c>, each percent-decoded once.
/// </summary>
/// <param name="Algorithm">The <c>X-Amz-Algorithm</c>, or <see langword="null"/> when absent.</param>
/// <param name="Authorization">The credential, signed headers and signature, or <see langword="null"/> when any is absent or malformed.</param>
/// <param name="RequestTime">The <c>X-Amz-Date</c>, or <see langword="null"/> when absent.</param>
/// <param name="Expires">The <c>X-Amz-Expires</c> as sent, or <see langword="null"/> when absent.</param>
internal sealed record AwsSigV4QueryString(string? Algorithm, AwsSigV4Authorization? Authorization, string? RequestTime, string? Expires)
{
    /// <summary>
    /// The query parameter carrying the signature, left out of the canonical query.
    /// </summary>
    public const string SignatureParameter = "X-Amz-Signature";

    private const string AlgorithmParameter = "X-Amz-Algorithm";

    /// <summary>
    /// The longest a presigned URL may be valid: seven days, in seconds.
    /// </summary>
    public const int MaxExpiresSeconds = 604800;

    /// <summary>
    /// Whether <paramref name="target"/> carries a presigned URL's <c>X-Amz-Algorithm</c>.
    /// </summary>
    /// <param name="target">The request target as received.</param>
    /// <returns><see langword="true"/> when the query names the algorithm.</returns>
    public static bool IsPresigned(string target) => ReadParameters(target).ContainsKey(AlgorithmParameter);

    /// <summary>
    /// Reads the parameters from <paramref name="target"/>'s query.
    /// </summary>
    /// <param name="target">The request target as received.</param>
    /// <returns>The parameters; any that is missing is <see langword="null"/>.</returns>
    public static AwsSigV4QueryString Read(string target)
    {
        var parameters = ReadParameters(target);
        var credential = parameters.GetValueOrDefault("X-Amz-Credential");
        var signedHeaders = parameters.GetValueOrDefault("X-Amz-SignedHeaders");
        var signature = parameters.GetValueOrDefault(SignatureParameter);
        var authorization = credential is null || signedHeaders is null || signature is null
            ? null
            : AwsSigV4Authorization.TryCreate(credential, signedHeaders, signature);

        return new AwsSigV4QueryString(
            parameters.GetValueOrDefault(AlgorithmParameter),
            authorization,
            parameters.GetValueOrDefault("X-Amz-Date"),
            parameters.GetValueOrDefault("X-Amz-Expires"));
    }

    /// <summary>
    /// The <c>X-Amz-Expires</c> as a duration when it is a whole number of seconds from 1 to
    /// <see cref="MaxExpiresSeconds"/>, else <see langword="null"/>.
    /// </summary>
    public TimeSpan? ExpiresAfter =>
        int.TryParse(Expires, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds is >= 1 and <= MaxExpiresSeconds
            ? TimeSpan.FromSeconds(seconds)
            : null;

    // Each name=value of the query, decoded once; the first of a repeated name is kept.
    private static Dictionary<string, string> ReadParameters(string target)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        var queryStart = target.IndexOf('?');
        var query = queryStart < 0 ? string.Empty : target[(queryStart + 1)..];
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries).Select(parameter => parameter.Split('=', 2)))
        {
            parameters.TryAdd(Uri.UnescapeDataString(pair[0]), pair.Length == 2 ? Uri.UnescapeDataString(pair[1]) : string.Empty);
        }

        return parameters;
    }
}
