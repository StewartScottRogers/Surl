using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// The features a test case's <c>&lt;features&gt;</c> part and its <c>%if</c> lines test, for a
/// pinned upstream curl build, named and derived as upstream's runtests
/// (<c>checksystemfeatures</c> in <c>tests/runtests.pl</c> at tag <c>curl-8_21_0</c>) derives
/// them from the build (ADR-0080 decision 3, "Features").
/// </summary>
/// <remarks>
/// Runtests reads some of them from <c>curlinfo</c>, a program only upstream's test build has;
/// <see cref="ReleaseBuildOptions"/> holds those a default release build of 8.21.0 enables,
/// measured against the Windows reference pin in BL-389. <c>Debug</c>, <c>TrackMemory</c> and
/// <c>unittest</c> are never present: the pinned builds are release builds.
/// </remarks>
public static class UpstreamCurlBuildFeatures
{
    /// <summary>
    /// The build options of a default release build that every pinned platform shares:
    /// <c>curlinfo</c>'s <c>ON</c> lines for a build with no <c>CURL_DISABLE_*</c>, and
    /// <c>manual</c> (<c>curl -M</c>) and <c>local-http</c> (Surl's servers listen on
    /// <c>127.0.0.1</c>).
    /// </summary>
    public static readonly IReadOnlyList<string> ReleaseBuildOptions =
    [
        "--libcurl", "aws", "basic-auth", "bearer-auth", "bindlocal", "cookies", "digest", "DoH",
        "form-api", "headers-api", "HTTP-auth", "large-size", "large-time", "local-http", "manual",
        "Mime", "negotiate-auth", "netrc", "parsedate", "proxy", "sha512-256", "shuffle-dns",
        "typecheck", "verbose-strings", "wakeup",
    ];

    /// <summary>
    /// The <c>Features:</c> line words runtests turns into a feature of the same name.
    /// </summary>
    private static readonly string[] FeaturesLineNames =
    [
        "alt-svc", "asyn-rr", "brotli", "CAcert", "ECH", "GSS-API", "HSTS", "HTTPS-proxy", "HTTPSRR",
        "IDN", "IPv6", "Kerberos", "Largefile", "libz", "MultiSSL", "NTLM", "NTLM_WB", "PSL", "SPNEGO",
        "SSL", "SSPI", "threadsafe", "TLS-SRP", "Unicode", "UnixSockets", "zstd",
    ];

    /// <summary>
    /// The words of the version line's <c>libcurl/</c> part and the features each one sets.
    /// </summary>
    private static readonly (string Word, string[] Features)[] LibraryWords =
    [
        ("Schannel", ["Schannel", "SSLpinning"]),
        ("OpenSSL", ["OpenSSL", "SSLpinning"]),
        ("LibreSSL", ["OpenSSL", "SSLpinning"]),
        ("BoringSSL", ["OpenSSL", "SSLpinning"]),
        ("AWS-LC", ["OpenSSL", "SSLpinning"]),
        ("quictls", ["OpenSSL", "SSLpinning"]),
        ("GnuTLS", ["GnuTLS", "SSLpinning"]),
        ("wolfSSL", ["wolfssl", "SSLpinning"]),
        ("mbedTLS", ["mbedtls", "SSLpinning"]),
        ("rustls", ["rustls"]),
        ("nghttp2", ["h2c"]),
        ("AppleIDN", ["AppleIDN"]),
        ("WinIDN", ["WinIDN"]),
        ("libidn2", ["libidn2"]),
        ("libssh2", ["libssh2", "sshkeyalgo"]),
        ("libssh/", ["libssh", "sshkeyalgo"]),
    ];

    /// <summary>
    /// Derives the features of <paramref name="build"/>.
    /// </summary>
    /// <param name="build">The pinned build.</param>
    /// <returns>The feature names, compared ordinally as runtests compares them.</returns>
    public static IReadOnlySet<string> For(PinnedUpstreamCurlBuild build)
    {
        ArgumentNullException.ThrowIfNull(build);

        var features = new HashSet<string>(ReleaseBuildOptions, StringComparer.Ordinal);
        var words = new HashSet<string>(build.Features, StringComparer.OrdinalIgnoreCase);

        features.UnionWith(FeaturesLineNames.Where(words.Contains));
        AddFromVersionLine(features, build.Version);
        AddRenamedFeatures(features, words);
        AddPlatformOptions(features);
        features.UnionWith(ProtocolFeatures(build.Protocols, features));

        return features;
    }

    private static void AddFromVersionLine(HashSet<string> features, string version)
    {
        var libcurlAt = version.IndexOf("libcurl/", StringComparison.Ordinal);
        var tool = libcurlAt < 0 ? version : version[..libcurlAt];
        var library = libcurlAt < 0 ? string.Empty : version[libcurlAt..];

        if (WindowsTool.IsMatch(tool))
        {
            features.Add("win32");
        }

        foreach (var (word, set) in LibraryWords.Where(entry => library.Contains(entry.Word, StringComparison.OrdinalIgnoreCase)))
        {
            features.UnionWith(set);
        }

        if (library.Contains("ares", StringComparison.OrdinalIgnoreCase))
        {
            features.Add("c-ares");
        }
    }

    private static void AddRenamedFeatures(HashSet<string> features, HashSet<string> words)
    {
        if (words.Contains("AsynchDNS") && (!features.Contains("c-ares") || words.Contains("asyn-rr")))
        {
            features.Remove("c-ares");
            features.Add("threaded-resolver");
            features.Add("resolv-threaded");
        }

        AddWhen(features, words.Contains("HTTP2"), "http/2");
        AddWhen(features, words.Contains("HTTP3"), "http/3");
        AddWhen(features, words.Contains("SSLS-EXPORT"), "ssl-sessions");
        AddWhen(features, features.Contains("NTLM") || features.Contains("Kerberos") || features.Contains("SPNEGO"), "crypto");
        AddWhen(features, features.Contains("OpenSSL") || features.Contains("GnuTLS"), "cert-status");
    }

    private static void AddPlatformOptions(HashSet<string> features)
    {
        var windows = features.Contains("win32");

        AddWhen(features, windows, "win32-ca-searchpath");
        AddWhen(features, !windows, "xattr");
        AddWhen(features, !windows, "getrlimit");
        AddWhen(features, !windows, "codeset-utf8");
    }

    private static List<string> ProtocolFeatures(IReadOnlyList<string> protocols, HashSet<string> features)
    {
        var names = protocols.Select(protocol => protocol.ToLowerInvariant()).ToList();
        var result = names.SelectMany(name => new[] { name, name + "-ipv6", name + "-unix" }).ToList();

        result.AddRange(["http-proxy", "https-mtls", "none"]);
        AddWhen(result, features.Contains("HTTPS-proxy"), "https-proxy");

        if (features.Contains("TLS-SRP") && names.Contains("https"))
        {
            result.AddRange(["httptls", "httptls-ipv6"]);
        }

        return result;
    }

    private static void AddWhen(ICollection<string> features, bool condition, string name)
    {
        if (condition)
        {
            features.Add(name);
        }
    }

    private static readonly Regex WindowsTool = new("win32|Windows|windows|mingw(32|64)", RegexOptions.CultureInvariant);
}
