using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Confirms, with the reference upstream curl build pinned for the platform the test runs on, each
/// build option <see cref="UpstreamCurlBuildFeatures.For(PinnedUpstreamCurlBuild)"/> claims for it
/// - <see cref="UpstreamCurlBuildFeatures.ReleaseBuildOptions"/> and the platform options - by a
/// probe command whose answer shows the option is built in (ADR-0080 decision 3, "Features";
/// BL-389 probed the Windows pin, BL-598 the Linux and macOS ones in CI). An option no command of
/// the curl tool can show is named in <see cref="NotShownByTheTool"/> with the reason. A failing run
/// lists every option whose probe disagreed and what the probe saw. Inconclusive where the pinned
/// build is not installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlShowsItsBuildOptionsTests
{
    // The options UpstreamCurlBuildFeatures adds by platform rather than from the version report.
    private static readonly string[] PlatformOptions = ["cert-status", "codeset-utf8", "getrlimit", "xattr"];

    /// <summary>
    /// The options no command of the curl tool shows, each with why; their claims rest on how
    /// <c>curlinfo</c> and runtests derive them, as BL-389's Notes record.
    /// </summary>
    private static readonly Dictionary<string, string> NotShownByTheTool = new(StringComparer.Ordinal)
    {
        ["codeset-utf8"] = "runtests asks whether the runner has a C.UTF-8 locale; it is not a property of the build",
        ["form-api"] = "the tool builds forms with the mime API, never the deprecated curl_formadd",
        ["getrlimit"] = "runtests reads HAVE_GETRLIMIT from the build tree's curl_config.h; the tool never calls getrlimit",
        ["large-size"] = "a 64-bit size_t changes no answer the tool gives",
        ["shuffle-dns"] = "the tool has no option that sets CURLOPT_DNS_SHUFFLE_ADDRESSES",
        ["typecheck"] = "the curl_easy_setopt type checks act at the build's compile time",
        ["wakeup"] = "curl_multi_wakeup is not called by the tool",
    };

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(30);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Probes_CoverEveryReleaseBuildAndPlatformOption()
    {
        var unprobed = ReleaseAndPlatformOptions()
            .Where(option => !NotShownByTheTool.ContainsKey(option) && !ProbeFor(option, new HashSet<string>(), null!).HasValue)
            .ToList();

        Assert.IsEmpty(unprobed, string.Join(", ", unprobed));
    }

    [TestMethod]
    public async Task Probe_EveryOptionTheReferencePinClaims_ShowsTheOptionBuiltIn()
    {
        var pins = UpstreamCurlBuildPins.Parse(await File.ReadAllTextAsync(
            Path.Combine(PinnedUpstreamCurl.RepositoryRoot(), UpstreamCurlBuildPins.FileName), TestContext.CancellationToken));
        var reference = pins.FirstOrDefault(pin => pin.Kind == UpstreamCurlBuildKind.Curl
            && pin.Platform == UpstreamCurlLocator.CurrentPlatform
            && pin.Role == UpstreamCurlBuildRole.Reference);
        if (reference is null)
        {
            Assert.Inconclusive($"UpstreamCurlBuilds.json pins no reference upstream curl for {UpstreamCurlLocator.CurrentPlatform}.");
        }

        var claimed = UpstreamCurlBuildFeatures.For(reference);
        using var home = new IsolatedCurlHome();

        var disagreements = new List<string>();
        foreach (var option in ReleaseAndPlatformOptions().Where(claimed.Contains))
        {
            var disagreement = await ProbeAsync(option, claimed, home);
            if (disagreement is not null)
            {
                disagreements.Add($"{option}: {disagreement}");
            }
        }

        Assert.IsEmpty(disagreements, string.Join('\n', disagreements));
    }

    private static List<string> ReleaseAndPlatformOptions() =>
        [.. UpstreamCurlBuildFeatures.ReleaseBuildOptions.Concat(PlatformOptions).Order(StringComparer.Ordinal)];

    // Runs the probe for option and returns what it saw when that does not show the option built
    // in, or null when it does or no command can show it.
    private async Task<string?> ProbeAsync(string option, IReadOnlySet<string> claimed, IsolatedCurlHome home)
    {
        if (NotShownByTheTool.TryGetValue(option, out var reason))
        {
            TestContext.WriteLine($"{option}: not probed, {reason}.");
            return null;
        }

        var probe = ProbeFor(option, claimed, home);
        if (probe is not { } shown)
        {
            return "no probe is written for it";
        }

        if (shown.Unobservable is { } why)
        {
            TestContext.WriteLine($"{option}: not probed on this build, {why}.");
            return null;
        }

        return await shown.Run!();
    }

    // The probe for each option, as BL-389 ran them against the Windows pin, made sharper where a
    // loopback listener can see what curl sent rather than only its exit code.
    private (Func<Task<string?>>? Run, string? Unobservable)? ProbeFor(string option, IReadOnlySet<string> claimed, IsolatedCurlHome home) =>
        option switch
        {
            "--libcurl" => (() => ExpectStandardOutputAsync(home, "curl_easy_init", "--libcurl", "-"), null),
            "aws" => (() => ExpectRequestAsync(home, "Authorization: AWS4-HMAC-SHA256 ", "--aws-sigv4", "aws:amz:us-east-1:s3", "-u", "key:secret"), null),
            "basic-auth" => (() => ExpectRequestAsync(home, "Authorization: Basic dXNlcjpwYXNz", "-u", "user:pass"), null),
            "bearer-auth" => (() => ExpectRequestAsync(home, "Authorization: Bearer probe-token", "--oauth2-bearer", "probe-token"), null),
            "bindlocal" => (() => ExpectLocalPortAsync(home), null),
            "cert-status" => (() => ExpectStatusRequestAsync(home), null),
            "cookies" => (() => ExpectRequestAsync(home, "Cookie: probe=1", "-b", "probe=1"), null),
            "digest" => (() => ExpectDigestAsync(home, "MD5"), null),
            "DoH" => (() => ExpectDohQueryAsync(home), null),
            "headers-api" => (() => ExpectStandardOutputAsync(home, "\"x-probe\":[\"1\"]", "-w", "%{header_json}"), null),
            "HTTP-auth" => (() => ExpectChallengeAnsweredAsync(home, "Basic realm=\"probe\"", "Authorization: Basic dXNlcjpwYXNz", "--anyauth"), null),
            "large-time" => (() => ExpectRequestAsync(home, "If-Modified-Since: Sun, 06 Nov 2050 08:49:37 GMT", "-z", "Sun, 06 Nov 2050 08:49:37 GMT"), null),
            "local-http" => (() => ExpectRequestAsync(home, "GET /probe HTTP/1.1"), null),
            "manual" => (() => ExpectManualAsync(home), null),
            "Mime" => (() => ExpectRequestAsync(home, "Content-Type: multipart/form-data; boundary=", "-F", "probe=1"), null),
            "negotiate-auth" => claimed.Contains("SPNEGO")
                ? (() => ExpectExitCodeAsync(home, 7, "--negotiate", "-u", ":", "http://127.0.0.1:1/"), null)
                : (null, "without SPNEGO (GSS-API or SSPI) libcurl has no Negotiate to offer, so no answer of the tool tells whether CURL_DISABLE_NEGOTIATE_AUTH is set"),
            "netrc" => (() => ExpectNetrcAsync(home), null),
            "parsedate" => (() => ExpectRequestAsync(home, "If-Modified-Since: Sun, 06 Nov 1994 08:49:37 GMT", "-z", "Sun, 06 Nov 1994 08:49:37 GMT"), null),
            "proxy" => (() => ExpectProxyAsync(home), null),
            "sha512-256" => claimed.Contains("SSPI")
                ? (null, "Digest goes through SSPI, which answers only MD5 (ADR-0032), whether or not libcurl has SHA-512/256")
                : (() => ExpectDigestAsync(home, "SHA-512-256"), null),
            "verbose-strings" => (() => ExpectStandardErrorAsync(home, "Trying 127.0.0.1:", "-v"), null),
            "xattr" => (() => ExpectExtendedAttributeAsync(home), null),
            _ => null,
        };

    private async Task<UpstreamCurlRunResult> RunAsync(IsolatedCurlHome home, params string[] arguments) =>
        await PinnedUpstreamCurl.RunWithEnvironmentAsync(TestContext, home.Environment, ["-q", "-sS", .. arguments]);

    private LoopbackHttpResponder StartResponder(Func<string, string>? answer = null) =>
        LoopbackHttpResponder.Start(answer ?? (_ => LoopbackHttpResponder.Ok("probed\n", "X-Probe: 1")), TestContext.CancellationToken);

    private async Task<string?> ExpectRequestAsync(IsolatedCurlHome home, string expectedLine, params string[] options)
    {
        await using var responder = StartResponder();

        var result = await RunAsync(home, [.. options, responder.UrlOf("probe")]);

        return SentLine(responder, expectedLine, result);
    }

    private async Task<string?> ExpectStandardOutputAsync(IsolatedCurlHome home, string expected, params string[] options)
    {
        await using var responder = StartResponder();

        var result = await RunAsync(home, [.. options, responder.UrlOf("probe")]);

        var output = Encoding.UTF8.GetString(result.StandardOutput);
        return result.ExitCode == 0 && output.Contains(expected, StringComparison.Ordinal)
            ? null
            : $"expected exit 0 and \"{expected}\" on standard output; {result}; stdout: {output}; stderr: {result.StandardError}";
    }

    private async Task<string?> ExpectStandardErrorAsync(IsolatedCurlHome home, string expected, params string[] options)
    {
        await using var responder = StartResponder();

        var result = await RunAsync(home, [.. options, responder.UrlOf("probe")]);

        return result.ExitCode == 0 && result.StandardError.Contains(expected, StringComparison.Ordinal)
            ? null
            : $"expected exit 0 and \"{expected}\" on standard error; {result}; stderr: {result.StandardError}";
    }

    private async Task<string?> ExpectExitCodeAsync(IsolatedCurlHome home, int expected, params string[] arguments)
    {
        var result = await RunAsync(home, arguments);

        return result.ExitCode == expected ? null : $"expected exit {expected}; {result}; stderr: {result.StandardError}";
    }

    private async Task<string?> ExpectManualAsync(IsolatedCurlHome home)
    {
        var result = await RunAsync(home, "-M");

        var output = Encoding.UTF8.GetString(result.StandardOutput);
        return result.ExitCode == 0 && output.Contains("--libcurl", StringComparison.Ordinal)
            ? null
            : $"expected exit 0 and the manual, which names --libcurl; {result}; stderr: {result.StandardError}";
    }

    // Once challenged with algorithm, curl must answer with a Digest Authorization naming it.
    private Task<string?> ExpectDigestAsync(IsolatedCurlHome home, string algorithm) =>
        ExpectChallengeAnsweredAsync(
            home,
            $"Digest realm=\"probe\", nonce=\"0123456789abcdef\", algorithm={algorithm}, qop=\"auth\"",
            $"algorithm={algorithm}",
            "--digest");

    // Answers every request that carries no Authorization with a 401 and challenge, and the
    // rest with a 200, so an option that waits for a challenge (--digest, --anyauth) is seen.
    private async Task<string?> ExpectChallengeAnsweredAsync(IsolatedCurlHome home, string challenge, string expectedLine, string option)
    {
        var unauthorized = $"HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: {challenge}\r\nContent-Length: 0\r\n\r\n";
        await using var responder = StartResponder(head => head.Contains("\r\nAuthorization: ", StringComparison.Ordinal)
            ? LoopbackHttpResponder.Ok("probed\n")
            : unauthorized);

        var result = await RunAsync(home, option, "-u", "user:pass", responder.UrlOf("probe"));

        return SentLine(responder, expectedLine, result);
    }

    private async Task<string?> ExpectNetrcAsync(IsolatedCurlHome home)
    {
        var netrc = await home.WriteFileAsync("probe.netrc", "machine 127.0.0.1 login user password pass\n"u8.ToArray(), TestContext.CancellationToken);

        return await ExpectRequestAsync(home, "Authorization: Basic dXNlcjpwYXNz", "--netrc-file", netrc);
    }

    private async Task<string?> ExpectProxyAsync(IsolatedCurlHome home)
    {
        await using var responder = StartResponder();

        var result = await RunAsync(home, "-x", responder.UrlOf(string.Empty), "http://probe.invalid/probe");

        return SentLine(responder, "GET http://probe.invalid/probe HTTP/1.1", result);
    }

    // A release build sends DoH queries over https only (an http DoH URL is never contacted, measured
    // 2026-11-06 on the Windows pin), so the probe is that curl opens a TLS connection to the DoH
    // URL. The recorder hangs up after the ClientHello, so the name never resolves and curl exits 6;
    // only that it asked counts. Once curl has exited, any ClientHello it sent has arrived.
    private async Task<string?> ExpectDohQueryAsync(IsolatedCurlHome home)
    {
        using var recorder = TlsClientHelloRecorder.Start(TestContext.CancellationToken);

        var result = await RunAsync(home, "-k", "--doh-url", recorder.UrlOf("dns-query"), "http://probe.invalid/probe");

        try
        {
            await recorder.ExtensionTypesAsync(TimeSpan.FromSeconds(5));
            return null;
        }
        catch (TimeoutException)
        {
            return $"expected a TLS connection to the DoH URL; {result}; stderr: {result.StandardError}";
        }
    }

    private async Task<string?> ExpectLocalPortAsync(IsolatedCurlHome home)
    {
        await using var responder = StartResponder();

        var result = await RunAsync(home, "--local-port", "41000-41999", responder.UrlOf("probe"));

        return result.ExitCode == 0 && responder.Requests is [{ ClientPort: >= 41000 and <= 41999 }]
            ? null
            : $"expected exit 0 and one request from a port in 41000-41999; {result}; stderr: {result.StandardError}; "
                + $"ports: {string.Join(", ", responder.Requests.Select(request => request.ClientPort))}";
    }

    // OpenSSL asks for a stapled OCSP response, the status_request extension (type 5, RFC 6066
    // section 8), only when curl sets CURLOPT_SSL_VERIFYSTATUS - which needs cert-status.
    private async Task<string?> ExpectStatusRequestAsync(IsolatedCurlHome home)
    {
        const int StatusRequest = 5;
        using var recorder = TlsClientHelloRecorder.Start(TestContext.CancellationToken);

        var result = await RunAsync(home, "--cert-status", "-k", recorder.UrlOf("probe"));
        var types = await recorder.ExtensionTypesAsync(ProbeTimeout);

        return types.Contains(StatusRequest)
            ? null
            : $"expected the ClientHello to carry status_request; extensions: {string.Join(", ", types)}; {result}; stderr: {result.StandardError}";
    }

    private async Task<string?> ExpectExtendedAttributeAsync(IsolatedCurlHome home)
    {
        await using var responder = StartResponder();
        var output = home.PathOf("xattr-probe.out");

        var result = await RunAsync(home, "--xattr", "-o", output, responder.UrlOf("probe"));

        var origin = result.ExitCode == 0 ? ExtendedFileAttributes.Read(output, "user.xdg.origin.url") : null;
        return origin == responder.UrlOf("probe")
            ? null
            : $"expected exit 0 and user.xdg.origin.url {responder.UrlOf("probe")}; it is {origin ?? "absent"}; {result}; stderr: {result.StandardError}";
    }

    private static string? SentLine(LoopbackHttpResponder responder, string expectedLine, UpstreamCurlRunResult result)
    {
        var sent = responder.Requests.Any(request => request.Head.Contains(expectedLine, StringComparison.Ordinal));
        return result.ExitCode == 0 && sent
            ? null
            : $"expected exit 0 and a request carrying \"{expectedLine}\"; {result}; stderr: {result.StandardError}; "
                + $"sent: {string.Join(" | ", responder.Requests.Select(request => request.Head.ReplaceLineEndings(" ")))}";
    }
}
