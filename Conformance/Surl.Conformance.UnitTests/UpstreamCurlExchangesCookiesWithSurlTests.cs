using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl against a <c>surl</c> configured with <c>--set-cookie</c> (ADR-0189, FR-015):
/// <c>-c</c> writes the cookie jar surl's <c>Set-Cookie</c> lines describe, <c>-b</c> sends the jar's cookies
/// back and <c>-b</c> with <c>-j</c> drops the session cookie, each received cookie read from surl's event
/// stream through the control endpoint's <c>cookie_received</c> events. The jar lines and the replayed order
/// were measured from curl 8.21.0 with <c>Record-CurlExchange.ps1</c> on 2026-11-24 (ADR-0189; BL-1662).
/// Inconclusive where no pinned build is installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlExchangesCookiesWithSurlTests
{
    private const long MaxAgeSeconds = 3600;

    // The control token: 32 to 1024 visible ASCII bytes on the first line of the token file.
    private const string Token = "cookie-control-token-0123456789abcdef";

    // The Expires instant of the "sid" and "dated" cookies: ahead of now, and inside the 400 days curl caps a
    // cookie's lifetime to.
    private readonly DateTimeOffset expires = DateTimeOffset.FromUnixTimeSeconds(
        TimeProvider.System.GetUtcNow().AddDays(30).ToUnixTimeSeconds());

    private string temporaryDirectory = null!;

    public TestContext TestContext { get; set; } = null!;

    private string Jar => Path.Combine(temporaryDirectory, "cookies.txt");

    [TestInitialize]
    public void CreateTemporaryDirectory() =>
        temporaryDirectory = Directory.CreateTempSubdirectory("surl-conformance-cookies-").FullName;

    [TestCleanup]
    public void DeleteTemporaryDirectory() => Directory.Delete(temporaryDirectory, recursive: true);

    [TestMethod]
    public async Task CookieJar_FromSurlsSetCookieLines_HoldsEveryAttributeCurlStores()
    {
        await using var surl = await StartAsync();
        var before = TimeProvider.System.GetUtcNow().ToUnixTimeSeconds();

        await WriteJarAsync(surl);

        var after = TimeProvider.System.GetUtcNow().ToUnixTimeSeconds();
        var lines = (await File.ReadAllLinesAsync(Jar, TestContext.CancellationToken))
            .Where(line => line.Length > 0 && !line.StartsWith("# ", StringComparison.Ordinal))
            .ToArray();
        Assert.HasCount(3, lines, string.Join('\n', lines));

        // Domain, include-subdomains, path, secure, expiry, name, value: SameSite is not stored.
        // Path and Expires are kept as set.
        Assert.AreEqual(
            string.Create(CultureInfo.InvariantCulture, $"127.0.0.1\tFALSE\t/app\tFALSE\t{expires.ToUnixTimeSeconds()}\tdated\td1"),
            lines[0]);
        // A session cookie has expiry 0.
        Assert.AreEqual("127.0.0.1\tFALSE\t/\tFALSE\t0\tsess\txyz", lines[1]);

        // HttpOnly is the line prefix, Secure the fourth field, and Max-Age wins over Expires.
        var sid = lines[2].Split('\t');
        CollectionAssert.AreEqual(
            new[] { "#HttpOnly_127.0.0.1", "FALSE", "/", "TRUE", sid[4], "sid", "abc123" }, sid, lines[2]);
        var expiry = long.Parse(sid[4], CultureInfo.InvariantCulture);
        // One second either side: curl's clock is coarser than this process's.
        Assert.IsTrue(
            expiry >= before + MaxAgeSeconds - 1 && expiry <= after + MaxAgeSeconds + 1,
            $"expiry {expiry} is not {MaxAgeSeconds} seconds after a time between {before} and {after}");
    }

    [TestMethod]
    public async Task CookieReplay_FromTheJar_PublishesEveryCookieOnSurlsEventStream()
    {
        await using var surl = await StartAsync();
        await WriteJarAsync(surl);

        var replay = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-b", Jar, surl.UrlOf("app/x"));

        Assert.AreEqual(0, replay.ExitCode, replay.StandardError);
        CollectionAssert.AreEqual(
            new[] { "dated=d1", "sess=xyz", "sid=abc123" }, await ReadReceivedCookiesAsync(surl, 3));
    }

    [TestMethod]
    public async Task CookieReplay_WithJunkSessionCookies_SendsOnlyThePersistentCookies()
    {
        await using var surl = await StartAsync();
        await WriteJarAsync(surl);

        var replay = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-b", Jar, "-j", surl.UrlOf("app/x"));

        Assert.AreEqual(0, replay.ExitCode, replay.StandardError);
        CollectionAssert.AreEqual(new[] { "dated=d1", "sid=abc123" }, await ReadReceivedCookiesAsync(surl, 2));
    }

    private async Task<SurlOnLoopback> StartAsync()
    {
        var tokenFile = Path.Combine(temporaryDirectory, "control.token");
        await File.WriteAllTextAsync(tokenFile, Token + "\n", TestContext.CancellationToken);
        var expiresText = expires.ToString("R", CultureInfo.InvariantCulture);

        return await SurlOnLoopback.StartInMemoryWithControlAsync(
            ["http"],
            [
                "--set-cookie",
                $"sid=abc123;Domain=127.0.0.1;Path=/;Expires={expiresText};Max-Age={MaxAgeSeconds};Secure;HttpOnly;SameSite=Lax",
                "--set-cookie", "sess=xyz;Path=/",
                "--set-cookie", $"dated=d1;Path=/app;Expires={expiresText};SameSite=Strict",
            ],
            tokenFile,
            TestContext.CancellationToken);
    }

    // The first run sends no Cookie header, so it publishes no cookie_received event.
    private async Task WriteJarAsync(SurlOnLoopback surl)
    {
        var write = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-c", Jar, surl.UrlOf(string.Empty));

        Assert.AreEqual(0, write.ExitCode, write.StandardError);
    }

    // Waits until surl has published expectedCount cookies, then lists every one it published, as name=value.
    private async Task<string[]> ReadReceivedCookiesAsync(SurlOnLoopback surl, int expectedCount)
    {
        var after = 0L;
        for (var received = 0; received < expectedCount; received++)
        {
            var waited = await ControlGetAsync(
                surl, $"/v1/events/wait?kind=cookie_received&after={after}&timeout_ms=10000");
            Assert.IsTrue(waited.TryGetProperty("event", out var next), waited.GetRawText());
            after = next.GetProperty("sequence").GetInt64();
        }

        var listed = await ControlGetAsync(surl, "/v1/events?kind=cookie_received");
        return [.. listed.GetProperty("events").EnumerateArray()
            .Select(cookie => $"{cookie.GetProperty("name").GetString()}={cookie.GetProperty("value").GetString()}")];
    }

    private async Task<JsonElement> ControlGetAsync(SurlOnLoopback surl, string path)
    {
        var answer = await PinnedUpstreamCurl.RunAsync(
            TestContext,
            "-sS", "--fail-with-body", "-H", $"Authorization: Bearer {Token}",
            $"{surl.ControlUrl!.AbsoluteUri.TrimEnd('/')}{path}");
        Assert.AreEqual(0, answer.ExitCode, answer.StandardError);

        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(answer.StandardOutput));
        return document.RootElement.Clone();
    }
}
