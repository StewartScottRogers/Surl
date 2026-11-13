using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned Curl port's <c>amqp1</c> client against a live <c>surl amqp1://</c> and <c>amqp1s://</c> (BL-515,
/// ADR-0083 decisions 1 to 3): send a message to an address and receive it, receive from an empty address, and a
/// refused bad login. Each expected value comes from AMQP 1.0 as <c>Documentation/Conformance/Extended/Amqp1.md</c>
/// lists it - a message sent to an address is held in the queue of that name and a receiver on it is given the
/// message once, a receiver on an empty queue is given nothing, a refused SASL outcome ends the connection - never
/// from what the port printed beyond that. Inconclusive when the pinned client is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortClientExchangesMessagesWithSurlOverAmqp1Tests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("Amqp1:Surl-Nodes-1, Amqp1:Messaging-3.2-2, Amqp1:Security-5.2.1-1: a message sent to an address is received whole from the queue of that name")]
    [DataRow("amqp1", false)]
    [DataRow("amqp1s", true)]
    public async Task SendThenReceive_OnTheSameAddress_GivesTheMessageBack(string scheme, bool tls)
    {
        await using var surl = await StartAsync(scheme, tls);
        var send = await SendAsync(surl, scheme, "alice:secret", "/q1", "hello amqp1");

        var receive = await ReceiveAsync(surl, scheme, "alice:secret", "/q1");

        AssertOutput(string.Empty, send, surl);
        AssertOutput("hello amqp1", receive, surl);
    }

    [TestMethod]
    [Description("Amqp1:Surl-Nodes-1: a receiver on an address nothing was sent to is given no message, so the client prints nothing")]
    public async Task Receive_FromAnEmptyAddress_PrintsNothing()
    {
        await using var surl = await StartAsync("amqp1", false);

        var receive = await ReceiveAsync(surl, "amqp1", "alice:secret", "/empty", seconds: 3);

        AssertNothingReceived(receive, surl);
    }

    [TestMethod]
    [Description("Amqp1:Surl-Nodes-1: a queue's message is delivered once, so a second receive finds it empty")]
    public async Task Receive_AfterTheMessageWasTaken_PrintsNothing()
    {
        await using var surl = await StartAsync("amqp1", false);
        await SendAsync(surl, "amqp1", "alice:secret", "/once", "only once");

        var first = await ReceiveAsync(surl, "amqp1", "alice:secret", "/once");
        var second = await ReceiveAsync(surl, "amqp1", "alice:secret", "/once", seconds: 3);

        AssertOutput("only once", first, surl);
        AssertNothingReceived(second, surl);
    }

    [TestMethod]
    [Description("Amqp1:Surl-Sasl-1: a wrong password is refused by the SASL outcome and the client sends nothing")]
    public async Task Login_WithAWrongPassword_IsRefused()
    {
        await using var surl = await StartAsync("amqp1", false);

        var send = await SendAsync(surl, "amqp1", "alice:wrong", "/refused", "never");
        var receive = await ReceiveAsync(surl, "amqp1", "alice:secret", "/refused", seconds: 3);

        var diagnostics = $"{send}; stderr: {send.StandardError}; surl: {surl.Log}";
        Assert.AreNotEqual(0, send.ExitCode, diagnostics);
        AssertNothingReceived(receive, surl);
    }

    private Task<SurlOnLoopback> StartAsync(string scheme, bool tls) =>
        SurlOnLoopback.StartInMemoryAsync(
            scheme,
            tls ? ["--user", "alice:secret", "--self-signed"] : ["--user", "alice:secret", "--allow-plaintext-auth"],
            TestContext.CancellationToken);

    private Task<CurlPortClientRunResult> ReceiveAsync(SurlOnLoopback surl, string scheme, string login, string address, int seconds = 30) =>
        PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-sS", "-k", "-m", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture), $"{scheme}://{login}@127.0.0.1:{surl.BaseUrl.Port}{address}");

    private Task<CurlPortClientRunResult> SendAsync(SurlOnLoopback surl, string scheme, string login, string address, string body) =>
        PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-sS", "-k", "-m", "30", "-d", body, $"{scheme}://{login}@127.0.0.1:{surl.BaseUrl.Port}{address}");

    // A receiver on an empty queue waits for a message (Amqp1:Core-2.6.7-3), so the client ends at its own -m time-out.
    private static void AssertNothingReceived(CurlPortClientRunResult result, SurlOnLoopback surl)
    {
        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardOutput).Trim(), diagnostics);
    }

    private static void AssertOutput(string expected, CurlPortClientRunResult result, SurlOnLoopback surl)
    {
        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        Assert.AreEqual(0, result.ExitCode, diagnostics);
        Assert.AreEqual(expected, Encoding.UTF8.GetString(result.StandardOutput).Trim(), diagnostics);
    }
}
