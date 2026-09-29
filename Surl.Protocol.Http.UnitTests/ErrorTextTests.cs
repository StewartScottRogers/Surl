using System.Text.RegularExpressions;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class ErrorTextTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("GET /file.txt HTTP/1.1\r\nHost: h\r\n\r\n")]
    [DataRow("HEAD /file.txt HTTP/1.1\r\nHost: h\r\n\r\n")]
    public async Task ContentStoreFailureWithAPathInItsMessage_NeverReachesTheClient(string request)
    {
        var secretPath = Path.Join(Root, "private", "file.txt");
        var message = $"Access to the path '{secretPath}' is denied.";
        var fileSystem = new ThrowingContentFileSystem(Root, new IOException(message));
        var connection = new InMemoryConnection([Ascii(request)]);

        await Server(fileSystem).ServeAsync(connection, Context(new RecordingExchangeLog(), new FixedTimeProvider(Now), TestContext.CancellationToken));

        var sent = Latin1(connection.WrittenBytes);
        Assert.DoesNotContain(secretPath, sent);
        Assert.DoesNotContain(Root, sent);
        Assert.DoesNotContain(message, sent);
        Assert.DoesNotContain("IOException", sent);
    }

    [TestMethod]
    [DataRow("GET /file.txt HTTP/1.1\r\nHost: h\r\nConnection: close\r\n\r\n", 0)]
    [DataRow("GET /missing HTTP/1.1\r\nHost: h\r\nConnection: close\r\n\r\n", 0)]
    [DataRow("GET /file.txt HTTP/1.1\r\n\r\n", 0)]
    [DataRow("GET /file.txt HTTP/1.1\r\nBad Field\r\n\r\n", 0)]
    [DataRow("POST /file.txt HTTP/1.1\r\nHost: h\r\n\r\n", 0)]
    [DataRow("BREW /file.txt HTTP/1.1\r\nHost: h\r\n\r\n", 0)]
    [DataRow("GET /file.txt HTTP/2.0\r\n\r\n", 0)]
    [DataRow("GET /file.txt HTTP/1.1\r\nHost: h\r\nX-Pad: aaaaaaaaaaaaaaaa\r\n\r\n", 40)]
    [DataRow("GET /file.txt HTTP/1.1\r\nHost: h\r\nContent-Length: 999999999\r\n\r\n", 0)]
    [DataRow("GET /file.txt HTTP/1.1\r\nHost: h\r\nTransfer-Encoding: chunked\r\n\r\nzz\r\n", 0)]
    public async Task EveryResponse_CarriesServerSurlExactly(string request, int maxRequestHeadBytes)
    {
        var limits = maxRequestHeadBytes == 0 ? null : ExchangeLimits.Default with { MaxRequestHeadBytes = maxRequestHeadBytes };

        var (connection, _) = await ServeAsync([Ascii(request)], TestContext.CancellationToken, limits);

        var serverFields = Regex.Matches(Latin1(connection.WrittenBytes), "^Server:(.*)\r$", RegexOptions.Multiline);
        Assert.HasCount(1, serverFields);
        Assert.AreEqual(" surl", serverFields[0].Groups[1].Value);
    }
}
