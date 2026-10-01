using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl tool completes WebSocket exchanges against a live, in-process
/// <c>surl</c> over <c>ws</c> and <c>wss</c>, every case of ADR-0071 decision 11: a file is
/// downloaded byte for byte, a directory listed only with <c>--list-directories</c>, every refusal
/// ends curl with 22 (it refuses any status but <c>101</c>, <c>401</c> included), only the logins
/// curl sends unasked - Basic, Bearer, AWS Signature Version 4 - get in, and surl's idle and
/// transfer deadlines end an echo with <c>CLOSE</c> 1001, whose code curl writes to its output.
/// Inconclusive where no pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlTalksToSurlOverWebSocketTests
{
    // CURLE_HTTP_RETURNED_ERROR: curl 8.21.0 ends a WebSocket transfer whose answer is not 101 with it.
    private const int HttpReturnedError = 22;

    // CURLE_OPERATION_TIMEDOUT: curl's own -m passed.
    private const int OperationTimedOut = 28;

    // ADR-0071 decision 11: 200000 bytes, so surl's message spans four 65536-byte frames.
    private static readonly byte[] FileBin = MakeFileBin();

    // The two bytes of surl's CLOSE 1001 ("going away"), which curl writes like any payload.
    private static readonly byte[] GoingAway = [0x03, 0xE9];

    private static readonly string Credentials = $"{AccountsFile.User}:{AccountsFile.Password}";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task File_Ws_ExitsZeroWithTheFilesBytes()
    {
        await using var surl = await StartSurlAsync("ws");

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", surl.UrlOf("file.bin"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(FileBin, result.StandardOutput);
    }

    [TestMethod]
    public async Task File_WsWithOutput_WritesTheFilesBytesToTheOutputFile()
    {
        await using var surl = await StartSurlAsync("ws");
        var output = Path.Combine(Path.GetTempPath(), $"surl-conformance-ws-{Guid.NewGuid():N}.bin");
        try
        {
            var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-o", output, surl.UrlOf("file.bin"));

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            CollectionAssert.AreEqual(FileBin, await File.ReadAllBytesAsync(output, TestContext.CancellationToken));
        }
        finally
        {
            File.Delete(output);
        }
    }

    [TestMethod]
    public async Task EmptyFile_Ws_ExitsZeroWritingOnlyTheStatus()
    {
        await using var surl = await StartSurlAsync("ws");

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-w", "%{http_code}", surl.UrlOf("empty.bin"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual("101", Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task File_Wss_ExitsZeroWithTheFilesBytes()
    {
        await using var surl = await StartSurlAsync("wss", "--self-signed");

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-k", surl.UrlOf("file.bin"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(FileBin, result.StandardOutput);
    }

    [TestMethod]
    public async Task MissingPath_Ws_IsRefusedWith404()
    {
        await using var surl = await StartSurlAsync("ws");

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", surl.UrlOf("missing"));

        AssertRefused(result, "404");
    }

    [TestMethod]
    public async Task Directory_WithoutListDirectories_IsRefusedWith404()
    {
        await using var surl = await StartSurlAsync("ws");

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", surl.UrlOf("sub/"));

        AssertRefused(result, "404");
    }

    [TestMethod]
    public async Task Directory_ListDirectories_ExitsZeroWithTheListing()
    {
        await using var surl = await StartSurlAsync("ws", "--list-directories");

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", surl.UrlOf("sub/"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual("a.txt\n", Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Head_Ws_IsRefusedWith405()
    {
        await using var surl = await StartSurlAsync("ws");

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-I", surl.UrlOf("file.bin"));

        AssertRefused(result, "405");
    }

    [TestMethod]
    public async Task NoCredentials_Account_IsRefusedWith401()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("ws", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", surl.UrlOf("file.bin"));

        AssertRefused(result, "401");
    }

    [TestMethod]
    public async Task BasicOverWs_Account_IsRefusedWith403()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("ws", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-u", Credentials, surl.UrlOf("file.bin"));

        AssertRefused(result, "403");
    }

    [TestMethod]
    public async Task BasicOverWs_AllowPlaintextAuth_ExitsZeroWithTheFilesBytes()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("ws", "--allow-plaintext-auth", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-u", Credentials, surl.UrlOf("file.bin"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(FileBin, result.StandardOutput);
    }

    [TestMethod]
    public async Task BasicOverWss_Account_ExitsZeroWithTheFilesBytes()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("wss", "--self-signed", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-k", "-u", Credentials, surl.UrlOf("file.bin"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(FileBin, result.StandardOutput);
    }

    [TestMethod]
    public async Task BasicOverWss_WrongPassword_IsRefusedWith401()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("wss", "--self-signed", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-k", "-u", $"{AccountsFile.User}:wrong", surl.UrlOf("file.bin"));

        AssertRefused(result, "401");
    }

    [TestMethod]
    public async Task BearerOverWss_Account_ExitsZeroWithTheFilesBytes()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("wss", "--self-signed", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-k", "--oauth2-bearer", AccountsFile.BearerToken, surl.UrlOf("file.bin"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(FileBin, result.StandardOutput);
    }

    [TestMethod]
    public async Task AwsSigV4OverWss_Account_ExitsZeroWithTheFilesBytes()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("wss", "--self-signed", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-k", "--aws-sigv4", "aws:amz:us-east-1:s3", "-u", Credentials, surl.UrlOf("file.bin"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(FileBin, result.StandardOutput);
    }

    [TestMethod]
    public async Task DigestOverWs_Account_IsRefusedAtTheChallenge()
    {
        // Measured (ADR-0071): curl 8.21.0 answers no challenge on a WebSocket upgrade.
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("ws", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "--digest", "-u", Credentials, surl.UrlOf("file.bin"));

        AssertRefused(result, "401");
    }

    [TestMethod]
    public async Task NtlmOverWs_Account_IsRefusedAtTheType2Challenge()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync(
            "ws", "--auth", "basic,ntlm", "--allow-plaintext-auth", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "--ntlm", "-u", Credentials, surl.UrlOf("file.bin"));

        AssertRefused(result, "401");
    }

    [TestMethod]
    public async Task Echo_IdleTimeout_ExitsZeroWithSurlsGoingAwayClose()
    {
        await using var surl = await SurlOnLoopback.StartInMemoryAsync(
            "ws", ["--ws-echo", "--idle-timeout", "2"], TestContext.CancellationToken);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", surl.UrlOf(string.Empty));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(GoingAway, result.StandardOutput);
    }

    [TestMethod]
    public async Task Echo_MaxTime_ExitsZeroWithSurlsGoingAwayClose()
    {
        await using var surl = await SurlOnLoopback.StartInMemoryAsync(
            "ws", ["--ws-echo", "--max-time", "2"], TestContext.CancellationToken);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", surl.UrlOf(string.Empty));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(GoingAway, result.StandardOutput);
    }

    [TestMethod]
    public async Task Echo_CurlsMaxTimeFirst_ExitsOperationTimedOut()
    {
        await using var surl = await SurlOnLoopback.StartInMemoryAsync(
            "ws", ["--ws-echo", "--idle-timeout", "30"], TestContext.CancellationToken);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-m", "2", surl.UrlOf(string.Empty));

        Assert.AreEqual(OperationTimedOut, result.ExitCode, result.StandardError);
    }

    private static void AssertRefused(UpstreamCurlRunResult result, string status)
    {
        Assert.AreEqual(HttpReturnedError, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, $"Refused WebSocket upgrade: {status}");
        Assert.IsEmpty(result.StandardOutput);
    }

    private static byte[] MakeFileBin()
    {
        var bytes = new byte[200000];
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = (byte)(index * 7 % 251);
        }

        return bytes;
    }

    private Task<SurlOnLoopback> StartSurlAsync(string scheme, params string[] options) =>
        SurlOnLoopback.StartAsync(
            scheme,
            new Dictionary<string, byte[]>
            {
                ["file.bin"] = FileBin,
                ["empty.bin"] = [],
                [Path.Combine("sub", "a.txt")] = "a\n"u8.ToArray(),
            },
            ["sub"],
            options,
            TestContext.CancellationToken);
}
