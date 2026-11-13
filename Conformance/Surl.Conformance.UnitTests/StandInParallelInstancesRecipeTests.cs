using System.Text;
using Surl.Api;

namespace Surl.Conformance;

/// <summary>
/// Recipe: a test suite runs several independent stand-ins in one process (BL-818). Two <see cref="SurlServer"/>s
/// start side by side with different in-memory files; the pinned upstream curl 8.21.0 fetches from both, and each
/// server records only its own exchanges and is affected only by its own faults. Inconclusive where no pinned
/// build is installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class StandInParallelInstancesRecipeTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(30);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task TwoServers_EachServesItsOwnFileAndRecordsOnlyItsOwnExchanges()
    {
        // Arrange: two servers on port 0, each with a different file at the same path.
        await using var first = await StartAsync("from the first\n");
        await using var second = await StartAsync("from the second\n");

        // Act: the code under test fetches from both at the same time, and the test waits for each exchange to end.
        var results = await Task.WhenAll(
            PinnedUpstreamCurl.RunAsync(TestContext, "-sS", UrlOf(first)),
            PinnedUpstreamCurl.RunAsync(TestContext, "-sS", UrlOf(second)));
        var ended = new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded };
        await first.WaitForEventAsync(ended, WaitLimit, TestContext.CancellationToken);
        await second.WaitForEventAsync(ended, WaitLimit, TestContext.CancellationToken);

        // Assert: each answer is its own server's file, and each record holds one exchange.
        Assert.AreEqual("from the first\n", Encoding.UTF8.GetString(results[0].StandardOutput));
        Assert.AreEqual("from the second\n", Encoding.UTF8.GetString(results[1].StandardOutput));
        Assert.HasCount(1, first.FindExchanges(new SurlEventQuery()));
        Assert.HasCount(1, second.FindExchanges(new SurlEventQuery()));
    }

    [TestMethod]
    public async Task FaultAddedToOneServer_DoesNotAffectTheOther()
    {
        // Arrange: two servers; only the first gets a rule dropping every connection.
        await using var first = await StartAsync("from the first\n");
        await using var second = await StartAsync("from the second\n");
        first.Faults.Add(SurlFault.DropConnection());

        // Act: the code under test fetches from both.
        var fromFirst = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", UrlOf(first));
        var fromSecond = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", UrlOf(second));

        // Assert: the first fails; the second answers as if no fault existed.
        Assert.AreNotEqual(0, fromFirst.ExitCode);
        Assert.AreEqual(0, fromSecond.ExitCode, fromSecond.StandardError);
        Assert.AreEqual("from the second\n", Encoding.UTF8.GetString(fromSecond.StandardOutput));
    }

    private static string UrlOf(SurlServer server)
    {
        var url = server.BoundUrls.Single();
        return $"http://{url.Host}:{url.Port}/file.txt";
    }

    private Task<SurlServer> StartAsync(string content)
    {
        var options = new SurlServerOptions { RecordExchanges = true };
        options.ListenUrls.Add("http://127.0.0.1:0");
        options.Files["file.txt"] = Encoding.UTF8.GetBytes(content);
        return SurlServer.StartAsync(options, TestContext.CancellationToken);
    }
}
