using System.Text;
using Surl.Api;

namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build fetches from a server started through <see cref="SurlServer"/> on a real
/// loopback socket, over HTTP/1.1 and FTP, exactly as it does from the <c>surl</c> executable, and the
/// server's exchange record holds the bytes curl sent (BL-618). Inconclusive where no pinned build is
/// installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlFetchesFromSurlApiServerTests
{
    private static readonly byte[] Hello = "hello from surl\n"u8.ToArray();
    private static readonly byte[] HelloWorld = "hello world\n"u8.ToArray();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Get_File_ExitsZeroWithTheFilesBytesAndRecordsTheRequest()
    {
        await using var server = await StartAsync("http", "hello.txt", Hello);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-s", new Uri(server.BoundUrl, "hello.txt").AbsoluteUri);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello, result.StandardOutput);
        var received = Encoding.ASCII.GetString(server.Exchanges[0].Received);
        StringAssert.StartsWith(received, "GET /hello.txt HTTP/1.1");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("--disable-epsv")]
    [DataRow("-P -")]
    [DataRow("-P - --disable-eprt")]
    public async Task Download_EachDataConnectionMode_WritesTheFileAndRecordsTheLogin(string options)
    {
        await using var server = await StartAsync("ftp", "a.txt", HelloWorld);
        var optionWords = options.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var result = await PinnedUpstreamCurl.RunAsync(
            TestContext, ["-sS", .. optionWords, new Uri(server.BoundUrl, "a.txt").AbsoluteUri]);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
        var received = Encoding.ASCII.GetString(server.Exchanges[0].Received);
        StringAssert.StartsWith(received, "USER anonymous");
    }

    private Task<SurlServer> StartAsync(string scheme, string fileName, byte[] content)
    {
        var options = new SurlServerOptions { RecordExchanges = true };
        options.ListenUrls.Add(scheme + "://127.0.0.1:0");
        options.Files[fileName] = content;
        options.AllowAnonymous = true;
        return SurlServer.StartAsync(options, TestContext.CancellationToken);
    }
}
