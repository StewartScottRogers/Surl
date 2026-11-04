using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl tool completes its RTSP request - one <c>OPTIONS *</c>, whatever the
/// options - against a live, in-process <c>surl rtsp://</c>, with the exit code and result each
/// case of ADR-0074 decision 11 expects: the answer, its head under <c>-i</c>, the logins (Basic
/// and Bearer refused in plain text unless <c>--allow-plaintext-auth</c>, Digest on one
/// connection) and the head limit. surl serves a fresh temporary directory holding
/// <c>clip.bin</c>. Inconclusive, with the pin named, where no pinned build of the platform lists
/// <c>rtsp</c>, as on macOS (ADR-0026).
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlTalksToSurlOverRtspTests
{
    private const string Account = "tester:secret";
    private const string WrongPassword = "tester:wrong";

    private const string Public =
        "Public: OPTIONS, DESCRIBE, ANNOUNCE, SETUP, PLAY, PAUSE, TEARDOWN, GET_PARAMETER, SET_PARAMETER, RECORD";

    private static readonly Dictionary<string, byte[]> ServedFiles = new()
    {
        ["clip.bin"] = "a clip of opaque octets\n"u8.ToArray(),
    };

    private static readonly string[] AccountOptions = ["--user", Account];
    private static readonly string[] PlaintextAccountOptions = ["--user", Account, "--allow-plaintext-auth"];

    private IsolatedCurlHome curlHome = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateCurlHome() => curlHome = new IsolatedCurlHome();

    [TestCleanup]
    public void DeleteCurlHome() => curlHome.Dispose();

    [TestMethod]
    public async Task Options_ServedRoot_Exits0WithNothingWritten()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync(surl.BaseUrl.AbsoluteUri);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task Options_Include_WritesTheHeadWithCSeqDateServerAndPublic()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync("-i", surl.UrlOf("clip.bin"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var head = Encoding.ASCII.GetString(result.StandardOutput).Split("\r\n");
        Assert.AreEqual("RTSP/1.0 200 OK", head[0]);
        CollectionAssert.Contains(head, "CSeq: 1");
        CollectionAssert.Contains(head, "Server: surl");
        CollectionAssert.Contains(head, Public);
        Assert.IsTrue(head.Any(line => line.StartsWith("Date: ", StringComparison.Ordinal)), string.Join('|', head));
    }

    [TestMethod]
    public async Task Options_MissingPathWithFieldsVerbose_Exits0AndSendsOptionsStar()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync("-v", "-H", "X-Test: 1", "-A", "agent/1", surl.UrlOf("missing"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "> OPTIONS * RTSP/1.0");
        StringAssert.Contains(result.StandardError, "> X-Test: 1");
        StringAssert.Contains(result.StandardError, "> User-Agent: agent/1");
    }

    [TestMethod]
    public async Task Options_WriteOutResponseCode_Writes200()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync("-w", "%{response_code}", surl.UrlOf("clip.bin"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual("200", Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Upload_UploadsOn_Exits0AndStoresNothingBecauseCurlSendsNoBody()
    {
        await using var surl = await StartSurlAsync("--allow-uploads");
        var upload = await curlHome.WriteFileAsync("up.sdp", "v=0\r\n"u8.ToArray(), TestContext.CancellationToken);

        var result = await RunCurlAsync("-T", upload, surl.UrlOf("up.sdp"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.IsFalse(File.Exists(Path.Combine(surl.ServedDirectory!, "up.sdp")));
    }

    [TestMethod]
    public async Task Options_AccountsButNoLogin_Exits22()
    {
        await using var surl = await StartSurlAsync(AccountOptions);

        var result = await RunCurlAsync("-f", "-w", "%{response_code}", surl.UrlOf("clip.bin"));

        Assert.AreEqual(22, result.ExitCode, result.StandardError);
        Assert.AreEqual("401", Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Options_BasicInPlainTextWithoutAllowPlaintextAuth_Exits22With403(bool basicNamed)
    {
        await using var surl = await StartSurlAsync(AccountOptions);
        string[] login = basicNamed ? ["--basic", "-u", Account] : ["-u", Account];

        var result = await RunCurlAsync([.. login, "-f", "-w", "%{response_code}", surl.UrlOf("clip.bin")]);

        Assert.AreEqual(22, result.ExitCode, result.StandardError);
        Assert.AreEqual("403", Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Options_BasicWithAllowPlaintextAuth_Exits0()
    {
        await using var surl = await StartSurlAsync(PlaintextAccountOptions);

        var result = await RunCurlAsync("-f", "-u", Account, surl.UrlOf("clip.bin"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    public async Task Options_BasicWrongPasswordWithAllowPlaintextAuth_Exits22With401()
    {
        await using var surl = await StartSurlAsync(PlaintextAccountOptions);

        var result = await RunCurlAsync("-f", "-w", "%{response_code}", "-u", WrongPassword, surl.UrlOf("clip.bin"));

        Assert.AreEqual(22, result.ExitCode, result.StandardError);
        Assert.AreEqual("401", Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Options_Digest_Exits0OnOneConnection()
    {
        await using var surl = await StartSurlAsync(AccountOptions);

        var result = await RunCurlAsync("-f", "--digest", "-u", Account, "-w", "%{num_connects}", surl.UrlOf("clip.bin"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual("1", Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Options_DigestWrongPassword_Exits22With401()
    {
        await using var surl = await StartSurlAsync(AccountOptions);

        var result = await RunCurlAsync("-f", "--digest", "-u", WrongPassword, "-w", "%{response_code}", surl.UrlOf("clip.bin"));

        Assert.AreEqual(22, result.ExitCode, result.StandardError);
        Assert.AreEqual("401", Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Options_AnyAuth_Exits0WithDigest()
    {
        await using var surl = await StartSurlAsync(AccountOptions);

        var result = await RunCurlAsync("-v", "-f", "--anyauth", "-u", Account, surl.UrlOf("clip.bin"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "> Authorization: Digest ");
    }

    [TestMethod]
    public async Task Options_BearerWithAllowPlaintextAuth_Exits0()
    {
        await using var surl = await StartSurlAsync("--user", ":tok", "--allow-plaintext-auth");

        var result = await RunCurlAsync("-f", "--oauth2-bearer", "tok", surl.UrlOf("clip.bin"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    public async Task Options_AllowAnonymous_Exits0WithoutALogin()
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--allow-anonymous"]);

        var result = await RunCurlAsync("-f", surl.UrlOf("clip.bin"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    public async Task Options_HeadPastMaxRequestHead_Exits22With431()
    {
        await using var surl = await StartSurlAsync("--max-request-head", "128");

        var result = await RunCurlAsync(
            "-f", "-w", "%{response_code}", "-H", $"X-Long: {new string('x', 200)}", surl.UrlOf("clip.bin"));

        Assert.AreEqual(22, result.ExitCode, result.StandardError);
        Assert.AreEqual("431", Encoding.ASCII.GetString(result.StandardOutput));
    }

    private Task<SurlOnLoopback> StartSurlAsync(params string[] options) =>
        SurlOnLoopback.StartAsync("rtsp", ServedFiles, [], options, TestContext.CancellationToken);

    private Task<UpstreamCurlRunResult> RunCurlAsync(params string[] arguments) =>
        PinnedUpstreamCurl.RunForProtocolAsync(TestContext, "rtsp", ["-sS", "-m", "20", .. arguments]);
}
