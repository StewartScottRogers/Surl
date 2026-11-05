namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Reads the bucket a virtual-hosted request names in its <c>Host</c> field ("XML API overview";
/// ADR-0110, decision 2): <c>&lt;bucket&gt;.&lt;domain&gt;</c> for the listen URL's host when it is
/// a DNS name, <c>localhost</c> or <c>storage.googleapis.com</c>. Any other host is path style.
/// </summary>
internal static class GcsVirtualHost
{
    private const string LocalhostDomain = "localhost";
    private const string GoogleDomain = "storage.googleapis.com";

    /// <summary>
    /// The bucket a request's authority names, or <see langword="null"/> when the request is path style.
    /// </summary>
    /// <param name="authority">The <c>Host</c> field's value, port included, or <see langword="null"/>.</param>
    /// <param name="listenHost">The listen URL's host.</param>
    /// <returns>The bucket, everything before the domain, dots included; or <see langword="null"/>.</returns>
    public static string? BucketOf(string? authority, string listenHost)
    {
        if (HostName(authority) is not { } host)
        {
            return null;
        }

        foreach (var domain in Domains(listenHost))
        {
            if (host.Length > domain.Length + 1 && host.EndsWith($".{domain}", StringComparison.Ordinal))
            {
                return host[..^(domain.Length + 1)];
            }
        }

        return null;
    }

    // The host's name: port removed, case folded, without a trailing dot; an IP literal in
    // brackets, or no host, is none.
    private static string? HostName(string? authority)
    {
        if (authority is null || authority.StartsWith('['))
        {
            return null;
        }

        var colon = authority.LastIndexOf(':');
        var name = colon < 0 ? authority : authority[..colon];

        return name.TrimEnd('.').ToLowerInvariant();
    }

    private static IEnumerable<string> Domains(string listenHost)
    {
        if (Uri.CheckHostName(listenHost) == UriHostNameType.Dns)
        {
            yield return listenHost.TrimEnd('.').ToLowerInvariant();
        }

        yield return LocalhostDomain;
        yield return GoogleDomain;
    }
}
