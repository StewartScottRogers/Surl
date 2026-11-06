using System.Text;

namespace Surl.Conformance;

/// <summary>
/// <see cref="ClientBytesRelay"/> against a live <c>surl http://</c> with the pinned upstream build.
/// The expected request is upstream curl 8.21.0's own, measured with <c>Record-CurlExchange.ps1</c> and
/// recorded in ADR-0007 (its <c>--trace-exchange</c> example): never the Curl port's.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class ClientBytesRelayTests
{
    private static readonly byte[] Hello = "hello from surl\n"u8.ToArray();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Transcript_UpstreamCurlGetsAFileThroughTheRelay_IsTheExactRequestAndTheFileArrives()
    {
        await using var surl = await SurlOnLoopback.StartAsync(new Dictionary<string, byte[]> { ["hello.txt"] = Hello }, TestContext.CancellationToken);
        await using var relay = ClientBytesRelay.Start(surl.BaseUrl, TestContext.CancellationToken);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-s", new Uri(relay.BaseUrlFor(surl.BaseUrl), "hello.txt").AbsoluteUri);
        await relay.WaitForConnectionsAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello, result.StandardOutput);
        var connections = relay.Transcript.ToConnections();
        Assert.HasCount(1, connections);
        Assert.AreEqual(
            $"GET /hello.txt HTTP/1.1\r\nHost: 127.0.0.1:{relay.Port}\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n",
            Encoding.Latin1.GetString(connections[0]));
    }
}
