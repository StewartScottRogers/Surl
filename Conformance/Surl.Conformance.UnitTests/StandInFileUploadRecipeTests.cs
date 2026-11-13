using Surl.Api;
using Surl.Protocol.Abstractions;

namespace Surl.Conformance;

/// <summary>
/// Recipe: a test suite uses one <see cref="SurlServer"/> as the file site behind the code it tests (BL-817).
/// The server runs <c>ftp</c>, <c>sftp</c> and <c>http</c> over one content store; the pinned upstream curl plays
/// the code under test, uploading over FTP and over SFTP and fetching over HTTP. Nothing sleeps or polls: the
/// test waits on the content store's write event with <see cref="SurlServer.WaitForEventAsync"/> and reads the
/// exchange that made it from the server's record. Inconclusive where no pinned build is installed, or where
/// the pinned build lists no SFTP.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class StandInFileUploadRecipeTests
{
    private const string User = "tester";
    private const string Password = "secret";

    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(30);
    private static readonly byte[] Content = "uploaded by the code under test\n"u8.ToArray();

    private readonly List<string> temporaryDirectories = [];

    public TestContext TestContext { get; set; } = null!;

    [TestCleanup]
    public void DeleteTemporaryDirectories()
    {
        foreach (var directory in temporaryDirectories)
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task UploadOverFtp_ThenFetchOverHttp_ReturnsTheUploadedBytes()
    {
        // Arrange: one server, three file protocols, uploads allowed, one account; the content store is shared.
        await using var server = await StartAsync();
        var file = await WriteUploadFileAsync();

        // Act: the code under test uploads over FTP; the test waits for the store's write event, then fetches
        // the file over HTTP.
        var uploaded = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-u", $"{User}:{Password}", "-T", file, UrlOf(server, "ftp", "/up.txt"));
        await server.WaitForEventAsync(ContentChange(), WaitLimit, TestContext.CancellationToken);
        var fetched = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", UrlOf(server, "http", "/up.txt"));

        // Assert: the write came in through the FTP listener and the HTTP bytes match.
        Assert.AreEqual(0, uploaded.ExitCode, uploaded.StandardError);
        StringAssert.Contains(System.Text.Encoding.ASCII.GetString(server.FindExchanges(new SurlEventQuery { Scheme = "ftp" }).Single().Received), "STOR up.txt");
        Assert.AreEqual(0, fetched.ExitCode, fetched.StandardError);
        CollectionAssert.AreEqual(Content, fetched.StandardOutput);
    }

    [TestMethod]
    public async Task UploadOverSftp_ThenFetchOverHttp_ReturnsTheUploadedBytes()
    {
        // Arrange: the same server and a file to upload.
        await using var server = await StartAsync();
        var file = await WriteUploadFileAsync();

        // Act: the code under test uploads over SFTP (-k: the host key is a throwaway), the test waits for the
        // write event, then fetches the file over HTTP.
        var uploaded = await PinnedUpstreamCurl.RunReferenceForProtocolAsync(
            TestContext, "sftp", "-sS", "-k", "-u", $"{User}:{Password}", "-T", file, UrlOf(server, "sftp", "/up.txt"));
        await server.WaitForEventAsync(ContentChange(), WaitLimit, TestContext.CancellationToken);
        var fetched = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", UrlOf(server, "http", "/up.txt"));

        // Assert: the write came in through the SFTP listener and the HTTP bytes match.
        Assert.AreEqual(0, uploaded.ExitCode, uploaded.StandardError);
        Assert.AreEqual("sftp", server.FindExchanges(new SurlEventQuery { Scheme = "sftp" }).Single().Scheme);
        Assert.AreEqual(0, fetched.ExitCode, fetched.StandardError);
        CollectionAssert.AreEqual(Content, fetched.StandardOutput);
    }

    private static SurlEventQuery ContentChange() =>
        new() { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Content };

    private static string UrlOf(SurlServer server, string scheme, string path)
    {
        var url = server.BoundUrls.Single(bound => bound.Scheme == scheme);
        return $"{scheme}://{url.Host}:{url.Port}{path}";
    }

    private Task<SurlServer> StartAsync()
    {
        var options = new SurlServerOptions { RecordExchanges = true, AllowUploads = true, ThrowawayHostKey = true, AllowAnonymous = true };
        foreach (var scheme in new[] { "ftp", "sftp", "http" })
        {
            options.ListenUrls.Add($"{scheme}://127.0.0.1:0");
        }

        options.Accounts.Add(new SurlAccount(User, Password));
        return SurlServer.StartAsync(options, TestContext.CancellationToken);
    }

    private async Task<string> WriteUploadFileAsync()
    {
        var directory = Directory.CreateTempSubdirectory("surl-recipe-upload-").FullName;
        temporaryDirectories.Add(directory);
        var path = Path.Combine(directory, "upload.txt");
        await File.WriteAllBytesAsync(path, Content, TestContext.CancellationToken);
        return path;
    }
}
