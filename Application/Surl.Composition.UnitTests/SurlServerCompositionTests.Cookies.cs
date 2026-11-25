using System.Text;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// <c>--set-cookie</c> reaching the composed HTTP server (ADR-0189, BL-1673) and HTTP/3 server (BL-1679), over
/// <see cref="FakeListenerFactory"/> and <see cref="StartTestSeams"/>, with no network.
/// </summary>
public sealed partial class SurlServerCompositionTests
{
    private const string ConfiguredSetCookieLine = "Set-Cookie: session=t1; Path=/; HttpOnly\r\n";
    private const string ConfiguredSetCookieField = "set-cookie: session=t1; Path=/; HttpOnly";

    [TestMethod]
    public async Task ComposeAsync_HttpWithASetCookie_AddsItsSetCookieLineToTheReply()
    {
        var reply = await ReplyToOneHttpGetAsync("--set-cookie", "session=t1;Path=/;HttpOnly");

        StringAssert.StartsWith(reply, "HTTP/1.1 ");
        StringAssert.Contains(reply, "\r\n" + ConfiguredSetCookieLine);
    }

    [TestMethod]
    public async Task ComposeAsync_HttpWithNoSetCookie_RepliesAsWithOneLessItsSetCookieLine()
    {
        var withCookie = await ReplyToOneHttpGetAsync("--set-cookie", "session=t1;Path=/;HttpOnly");

        var without = await ReplyToOneHttpGetAsync();

        Assert.DoesNotContain("Set-Cookie", without);
        Assert.AreEqual(withCookie.Replace(ConfiguredSetCookieLine, string.Empty, StringComparison.Ordinal), without);
    }

    [TestMethod]
    public async Task ComposeAsync_Http3WithASetCookie_AddsItsSetCookieFieldToTheReply()
    {
        var fields = await ReplyFieldsToOneHttp3GetAsync("--set-cookie", "session=t1;Path=/;HttpOnly");

        StringAssert.StartsWith(fields[0], ":status: ");
        CollectionAssert.Contains(fields, ConfiguredSetCookieField);
    }

    [TestMethod]
    public async Task ComposeAsync_Http3WithNoSetCookie_RepliesAsWithOneLessItsSetCookieField()
    {
        var withCookie = await ReplyFieldsToOneHttp3GetAsync("--set-cookie", "session=t1;Path=/;HttpOnly");

        var without = await ReplyFieldsToOneHttp3GetAsync();

        Assert.IsFalse(without.Any(field => field.StartsWith("set-cookie", StringComparison.Ordinal)), string.Join("|", without));
        CollectionAssert.AreEqual(withCookie.Where(field => field != ConfiguredSetCookieField).ToList(), without);
    }

    // Starts https with HTTP/3, sends one GET over a hand-built QUIC client, and returns the reply's header fields,
    // each as "name: value", all but date: the QUIC connection cannot close on a clock that stands still.
    private async Task<List<string>> ReplyFieldsToOneHttp3GetAsync(params string[] options)
    {
        using var client = new CompositionHttp3QuicClient();
        var flows = Channel.CreateUnbounded<IDatagramFlow>();
        flows.Writer.TryWrite(client.Flow);
        var seams = new StartTestSeams(new FakeListenerFactory { DatagramFlows = flows });
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse([.. options, "--allow-anonymous", "--self-signed", "--http3", "https://127.0.0.1:0"]),
            seams.Seams,
            TestContext.CancellationToken);
        await using var serving = start.Serving!;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        patience.CancelAfter(TimeSpan.FromSeconds(30));
        var serve = serving.ServeAsync(StartTestSeams.Outputs(TextWriter.Null), stop.Token);

        await client.GetAsync("/absent", patience.Token);
        var fields = await client.ResponseFieldsAsync(patience.Token);
        await stop.CancelAsync();
        await serve;

        return [.. fields.Where(field => field.Name != "date").Select(field => $"{field.Name}: {field.Value}")];
    }

    // The clock stands still, so two starts write the same Date.
    private async Task<string> ReplyToOneHttpGetAsync(params string[] options)
    {
        var connection = new FakeConnection(Encoding.ASCII.GetBytes("GET /absent HTTP/1.1\r\nHost: x\r\nConnection: close\r\n\r\n"));
        var seams = new StartTestSeams(new FakeListenerFactory
        {
            ConnectionsByScheme = new Dictionary<string, Task<FakeConnection>> { ["http"] = Task.FromResult(connection) },
        });
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse([.. options, "--allow-anonymous", "http://127.0.0.1:0/"]),
            seams.Seams with { TimeProvider = new SteppingTimeProvider() },
            TestContext.CancellationToken);
        await using var serving = start.Serving!;

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serve = serving.ServeAsync(StartTestSeams.Outputs(TextWriter.Null), stop.Token);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        return Encoding.ASCII.GetString(connection.WrittenBytes);
    }
}
