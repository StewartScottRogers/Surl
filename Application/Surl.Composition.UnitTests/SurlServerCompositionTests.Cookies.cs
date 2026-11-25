using System.Text;

namespace Surl.Composition;

/// <summary>
/// <c>--set-cookie</c> reaching the composed HTTP server (ADR-0189, BL-1673), over
/// <see cref="FakeListenerFactory"/> and <see cref="StartTestSeams"/>, with no network.
/// </summary>
public sealed partial class SurlServerCompositionTests
{
    private const string ConfiguredSetCookieLine = "Set-Cookie: session=t1; Path=/; HttpOnly\r\n";

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
