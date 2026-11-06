using Surl.Http3;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// The <c>origin</c> and <c>:path</c> rules a WebTransport extended <c>CONNECT</c> is judged by
/// (ADR-0117 decision 1; WTH3 3.2), and the statuses the server answers with;
/// <see cref="WebTransportConnectHandler"/> judges the login between the two (decision 7).
/// </summary>
internal static class WebTransportConnectJudgement
{
    /// <summary>The session is accepted.</summary>
    public const int Accepted = 200;

    /// <summary>The <c>origin</c> is not the server's own (WTH3 3.2: SHOULD answer 403).</summary>
    public const int Forbidden = 403;

    /// <summary>The <c>:path</c> is not served (WTH3 3.2: a resource without WebTransport SHOULD get 405).</summary>
    public const int MethodNotAllowed = 405;

    private const string SecureOriginPrefix = "https://";
    private const string DefaultPortSuffix = ":443";

    /// <summary>
    /// Whether the request carries no <c>origin</c>, or exactly one naming <c>https://</c> and the
    /// <c>:authority</c>, the host compared case-insensitively and a <c>:443</c> port treated as absent.
    /// </summary>
    /// <param name="head">The request's head.</param>
    /// <returns><see langword="true"/> when the origin is acceptable.</returns>
    public static bool IsOwnOrigin(Http3RequestHead head)
    {
        var origins = head.Fields.Where(field => field.Key == "origin").Select(field => field.Value).ToList();
        return origins.Count == 0 || (origins.Count == 1 && IsOrigin(origins[0], head.Authority ?? string.Empty));
    }

    /// <summary>
    /// Whether <paramref name="target"/>, without its query, is <paramref name="acceptedPath"/> or
    /// below it at a <c>/</c> boundary.
    /// </summary>
    /// <param name="target">The <c>:path</c>.</param>
    /// <param name="acceptedPath">The listen URL's path; empty or <c>/</c> accepts every path.</param>
    /// <returns><see langword="true"/> when the path is served.</returns>
    public static bool IsAcceptedPath(string target, string acceptedPath)
    {
        var path = target.Split('?')[0];
        var root = acceptedPath.TrimEnd('/');
        return root.Length == 0 || path == root || path.StartsWith(root + "/", StringComparison.Ordinal);
    }

    private static bool IsOrigin(string origin, string authority) =>
        origin.StartsWith(SecureOriginPrefix, StringComparison.OrdinalIgnoreCase)
        && string.Equals(WithoutDefaultPort(origin[SecureOriginPrefix.Length..]), WithoutDefaultPort(authority), StringComparison.OrdinalIgnoreCase);

    private static string WithoutDefaultPort(string hostAndPort) =>
        hostAndPort.EndsWith(DefaultPortSuffix, StringComparison.Ordinal) ? hostAndPort[..^DefaultPortSuffix.Length] : hostAndPort;
}
