namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Reads the bucket a virtual-hosted request names in its <c>Host</c> field ("Virtual hosting of
/// buckets"; ADR-0108, decision 2): <c>&lt;bucket&gt;.&lt;domain&gt;</c> for the listen URL's host
/// when it is a DNS name, <c>localhost</c>, <c>s3.amazonaws.com</c> or
/// <c>s3.&lt;region&gt;.amazonaws.com</c>. Any other host is path style.
/// </summary>
internal static class S3VirtualHost
{
    private const string LocalhostDomain = "localhost";
    private const string AwsGlobalDomain = "s3.amazonaws.com";
    private const string AwsDomainSuffix = ".amazonaws.com";
    private const string AwsServiceLabel = ".s3.";

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

        return RegionalAwsBucket(host);
    }

    // <bucket>.s3.<region>.amazonaws.com, the region one label.
    private static string? RegionalAwsBucket(string host)
    {
        if (!host.EndsWith(AwsDomainSuffix, StringComparison.Ordinal))
        {
            return null;
        }

        var beforeDomain = host[..^AwsDomainSuffix.Length];
        var service = beforeDomain.LastIndexOf(AwsServiceLabel, StringComparison.Ordinal);
        var isRegional = service > 0 && !beforeDomain[(service + AwsServiceLabel.Length)..].Contains('.');

        return isRegional ? beforeDomain[..service] : null;
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
        yield return AwsGlobalDomain;
    }
}
