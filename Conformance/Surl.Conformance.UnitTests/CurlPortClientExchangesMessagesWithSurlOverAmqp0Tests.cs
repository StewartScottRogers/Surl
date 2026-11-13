using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned Curl port's <c>amqp</c> client against a live <c>surl amqp://</c> and <c>amqps://</c> (BL-508,
/// ADR-0083 decisions 1 to 3): publish a message to the default exchange and get it back with <c>basic.get</c>, get
/// from an empty queue, and a refused bad login. Each expected value comes from AMQP 0-9-1 as
/// <c>Documentation/Conformance/Extended/Amqp0.md</c> lists it - a publish to the default exchange reaches the queue
/// named by its routing key, <c>basic.get</c> answers the message or <c>get-empty</c>, a refused login is
/// <c>connection.close</c> 403 - never from what the port printed beyond that. Inconclusive when the pinned client is
/// absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortClientExchangesMessagesWithSurlOverAmqp0Tests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("Amqp0:Basic-publish-1, Amqp0:Exchange-default-1, Amqp0:Basic-get-1, Amqp0:Surl-Tls-1: a message published to a queue is read back whole by basic.get")]
    [DataRow("amqp", false)]
    [DataRow("amqps", true)]
    public async Task PublishThenGet_OnTheSameQueue_GivesTheMessageBack(string scheme, bool tls)
    {
        await using var surl = await StartAsync(scheme, tls);
        var upload = await UploadAsync(surl, scheme, "alice:secret", "/q1", "hello amqp");

        var get = await RunAsync(surl, scheme, "alice:secret", "/q1");

        AssertOutput(string.Empty, upload, surl);
        AssertOutput("hello amqp", get, surl);
    }

    [TestMethod]
    [Description("Amqp0:Basic-get-1: basic.get on an empty queue answers get-empty, so the client prints nothing and succeeds")]
    public async Task Get_FromAnEmptyQueue_PrintsNothing()
    {
        await using var surl = await StartAsync("amqp", false);

        var get = await RunAsync(surl, "amqp", "alice:secret", "/empty");

        AssertOutput(string.Empty, get, surl);
    }

    [TestMethod]
    [Description("Amqp0:Basic-get-1: a message is given once, so a second get finds the queue empty")]
    public async Task Get_AfterTheMessageWasTaken_PrintsNothing()
    {
        await using var surl = await StartAsync("amqp", false);
        await UploadAsync(surl, "amqp", "alice:secret", "/once", "only once");

        var first = await RunAsync(surl, "amqp", "alice:secret", "/once");
        var second = await RunAsync(surl, "amqp", "alice:secret", "/once");

        AssertOutput("only once", first, surl);
        AssertOutput(string.Empty, second, surl);
    }

    [TestMethod]
    [Description("Amqp0:Surl-Login-3: a wrong password is refused with connection.close 403 and the client publishes nothing")]
    public async Task Login_WithAWrongPassword_IsRefused()
    {
        await using var surl = await StartAsync("amqp", false);

        var upload = await UploadAsync(surl, "amqp", "alice:wrong", "/refused", "never");
        var get = await RunAsync(surl, "amqp", "alice:secret", "/refused");

        var diagnostics = $"{upload}; stderr: {upload.StandardError}; surl: {surl.Log}";
        Assert.AreNotEqual(0, upload.ExitCode, diagnostics);
        AssertOutput(string.Empty, get, surl);
    }

    private Task<SurlOnLoopback> StartAsync(string scheme, bool tls) =>
        SurlOnLoopback.StartInMemoryAsync(
            scheme,
            tls ? ["--user", "alice:secret", "--self-signed"] : ["--user", "alice:secret", "--allow-plaintext-auth"],
            TestContext.CancellationToken);

    private Task<CurlPortClientRunResult> RunAsync(SurlOnLoopback surl, string scheme, string login, string queue) =>
        PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-sS", "-k", "-m", "30", $"{scheme}://{login}@127.0.0.1:{surl.BaseUrl.Port}/%2F{queue}");

    // The port publishes the -d data; its -T file upload to amqp:// published nothing on surl (BL-508 Notes).
    private Task<CurlPortClientRunResult> UploadAsync(SurlOnLoopback surl, string scheme, string login, string queue, string body) =>
        PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-sS", "-k", "-m", "30", "-d", body, $"{scheme}://{login}@127.0.0.1:{surl.BaseUrl.Port}/%2F{queue}");

    private static void AssertOutput(string expected, CurlPortClientRunResult result, SurlOnLoopback surl)
    {
        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        Assert.AreEqual(0, result.ExitCode, diagnostics);
        Assert.AreEqual(expected, Encoding.UTF8.GetString(result.StandardOutput).Trim(), diagnostics);
    }
}
