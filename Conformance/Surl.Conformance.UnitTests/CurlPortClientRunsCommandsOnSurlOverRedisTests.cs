using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned Curl port's <c>redis</c> client against a live <c>surl redis://</c> and <c>rediss://</c> (BL-493,
/// ADR-0083 decisions 1 to 3): <c>SET</c> then <c>GET</c> with a login, the same over RESP3 (the port's client opens
/// every connection with <c>HELLO 3 AUTH</c>), a database <c>SELECT</c>ed from the URL and a refused wrong password.
/// Each expected value comes from the RESP specification and the command pages as
/// <c>Documentation/Conformance/Extended/Redis.md</c> lists them - <c>SET</c> answers <c>OK</c>, <c>GET</c> the value,
/// databases are isolated, a wrong password is <c>WRONGPASS</c> - never from what the port printed beyond the bytes
/// the server was required to send. Inconclusive when the pinned client is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortClientRunsCommandsOnSurlOverRedisTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("Redis:AUTH-1, Redis:HELLO-4, Redis:SET-1, Redis:GET-1: after a login by HELLO 3 AUTH, SET answers OK and GET on a second connection answers the value")]
    [DataRow("redis", false)]
    [DataRow("rediss", true)]
    public async Task SetThenGet_AfterALogin_AnswersOkThenTheValue(string scheme, bool tls)
    {
        await using var surl = await StartAsync(scheme, tls);

        var set = await RunAsync(surl, scheme, "alice:secret", "/SET/greeting/hello%20redis");
        var get = await RunAsync(surl, scheme, "alice:secret", "/GET/greeting");

        AssertOutput("OK", set, surl);
        AssertOutput("hello redis", get, surl);
    }

    [TestMethod]
    [Description("Redis:HELLO-1, Redis:HELLO-2, Redis:RESP-Null-1, Redis:GET-1: the client speaks RESP3, so a GET of a missing key is the RESP3 null and prints nothing, and the connection is logged in")]
    public async Task Get_OfAMissingKeyOverRespThree_PrintsNothing()
    {
        await using var surl = await StartAsync("redis", false);

        var get = await RunAsync(surl, "redis", "alice:secret", "/GET/absent");

        AssertOutput(string.Empty, get, surl);
    }

    [TestMethod]
    [Description("Redis:SELECT-1, Redis:Surl-Databases-1: a key set in the database the URL selects is absent in another and present when that database is selected again")]
    public async Task Select_FromTheUrlPath_IsolatesDatabases()
    {
        await using var surl = await StartAsync("redis", false);

        var set = await RunAsync(surl, "redis", "alice:secret", "/3/SET/key/in-three");
        var sameDatabase = await RunAsync(surl, "redis", "alice:secret", "/3/GET/key");
        var otherDatabase = await RunAsync(surl, "redis", "alice:secret", "/4/GET/key");

        AssertOutput("OK", set, surl);
        AssertOutput("in-three", sameDatabase, surl);
        AssertOutput(string.Empty, otherDatabase, surl);
    }

    [TestMethod]
    [Description("Redis:AUTH-2, Redis:HELLO-4: a wrong password is refused with WRONGPASS and the client reaches no command")]
    public async Task Login_WithAWrongPassword_IsRefusedWithWrongPass()
    {
        await using var surl = await StartAsync("redis", false);

        var set = await RunAsync(surl, "redis", "alice:wrong", "/SET/greeting/never");
        var get = await RunAsync(surl, "redis", "alice:secret", "/GET/greeting");

        var diagnostics = $"{set}; stderr: {set.StandardError}; surl: {surl.Log}";
        Assert.AreNotEqual(0, set.ExitCode, diagnostics);
        StringAssert.Contains(set.StandardError, "WRONGPASS", diagnostics);
        AssertOutput(string.Empty, get, surl);
    }

    private Task<SurlOnLoopback> StartAsync(string scheme, bool tls) =>
        SurlOnLoopback.StartInMemoryAsync(
            scheme,
            tls ? ["--user", "alice:secret", "--self-signed"] : ["--user", "alice:secret", "--allow-plaintext-auth"],
            TestContext.CancellationToken);

    private Task<CurlPortClientRunResult> RunAsync(SurlOnLoopback surl, string scheme, string login, string path) =>
        PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-sS", "-k", "-m", "30", $"{scheme}://{login}@127.0.0.1:{surl.BaseUrl.Port}{path}");

    private static void AssertOutput(string expected, CurlPortClientRunResult result, SurlOnLoopback surl)
    {
        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        Assert.AreEqual(0, result.ExitCode, diagnostics);
        Assert.AreEqual(expected, Encoding.UTF8.GetString(result.StandardOutput).Trim(), diagnostics);
    }
}
