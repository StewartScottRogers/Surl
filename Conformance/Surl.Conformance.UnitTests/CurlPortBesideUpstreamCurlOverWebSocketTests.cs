using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// Phase 7 for WebSocket (ADR-0089, BL-585): every <c>ws</c> and <c>wss</c> conversation
/// <see cref="UpstreamCurlTalksToSurlOverWebSocketTests"/> runs, through pinned upstream curl and the
/// pinned Curl port, each against its own fresh <c>surl</c>, judged by
/// <see cref="PinnedCurlPortBesideUpstreamCurl"/>. One test per mirrored test, each naming it in its
/// <see cref="DescriptionAttribute"/>. A test fails when upstream's run is not what the mirrored test
/// asserts (a Surl defect), when the port disagrees and <c>Documentation/Conformance/CurlPort/WebSocket.md</c>
/// does not list the case, and when a listed case agrees (ADR-0089 decision 5). Every <c>ws</c> case
/// records the bytes each client sent, its upgrade request and its masked frames, compared as ADR-0089
/// decision 4 normalises them; <c>wss</c> cases compare everything but those bytes. The libcurl rows
/// of <see cref="PinnedLibcurlTalksToSurlOverWebSocketTests"/> are BL-597's (ADR-0089 decision 1 (c)).
/// Inconclusive when either pinned binary is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortBesideUpstreamCurlOverWebSocketTests
{
    private const string FindingsFile = "Documentation/Conformance/CurlPort/WebSocket.md";

    private const string Mirrored = nameof(UpstreamCurlTalksToSurlOverWebSocketTests);

    // CURLE_HTTP_RETURNED_ERROR: curl 8.21.0 ends a WebSocket transfer whose answer is not 101 with it.
    private const int HttpReturnedError = 22;

    // CURLE_OPERATION_TIMEDOUT: curl's own -m passed.
    private const int OperationTimedOut = 28;

    // ADR-0071 decision 11: 200000 bytes, so surl's message spans four 65536-byte frames.
    private static readonly byte[] FileBin = MakeFileBin();

    // The two bytes of surl's CLOSE 1001 ("going away"), which curl writes like any payload.
    private static readonly byte[] GoingAway = [0x03, 0xE9];

    private static readonly string Credentials = $"{AccountsFile.User}:{AccountsFile.Password}";

    private static readonly Dictionary<string, byte[]> ServedFiles = new()
    {
        ["file.bin"] = FileBin,
        ["empty.bin"] = [],
        ["sub/a.txt"] = "a\n"u8.ToArray(),
    };

    private static readonly Dictionary<string, byte[]> NoFiles = [];

    private IsolatedCurlHome localFiles = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateLocalFiles() => localFiles = new IsolatedCurlHome();

    [TestCleanup]
    public void DeleteLocalFiles() => localFiles.Dispose();

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverWebSocketTests.File_Ws_ExitsZeroWithTheFilesBytes")]
    public Task File_Ws_ExitsZeroWithTheFilesBytes() =>
        JudgeAsync(Case(Named(), "ws", ["{surl}/file.bin"], [], ServedFiles, ExitsZeroWriting(FileBin)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverWebSocketTests.File_WsWithOutput_WritesTheFilesBytesToTheOutputFile")]
    public async Task File_WsWithOutput_WritesTheFilesBytesToTheOutputFile()
    {
        // Both runs write the one output file in turn; each run's bytes are read before the next run starts.
        var output = localFiles.PathOf("out.bin");
        var written = new List<byte[]>();
        await JudgeAsync(
            Case(Named(), "ws", ["-o", output, "{surl}/file.bin"], [], ServedFiles, run =>
                run.ExitCode == 0 && written.Count > 0 && written[0].AsSpan().SequenceEqual(FileBin) ? null : Describe(run)),
            (_, _) => Task.FromResult<IAsyncDisposable>(new DisposeAction(() =>
            {
                written.Add(File.Exists(output) ? File.ReadAllBytes(output) : []);
                File.Delete(output);
            })));
        Assert.HasCount(2, written);
        CollectionAssert.AreEqual(written[0], written[1], "The Curl port's output file differs from upstream curl's.");
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverWebSocketTests.EmptyFile_Ws_ExitsZeroWritingOnlyTheStatus")]
    public Task EmptyFile_Ws_ExitsZeroWritingOnlyTheStatus() =>
        JudgeAsync(Case(Named(), "ws", ["-w", "%{http_code}", "{surl}/empty.bin"], [], ServedFiles, run =>
            run.ExitCode == 0 && Encoding.ASCII.GetString(run.StandardOutput) == "101" ? null : Describe(run)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverWebSocketTests.File_Wss_ExitsZeroWithTheFilesBytes")]
    public Task File_Wss_ExitsZeroWithTheFilesBytes() =>
        JudgeAsync(Case(Named(), "wss", ["-k", "{surl}/file.bin"], ["--self-signed"], ServedFiles, ExitsZeroWriting(FileBin)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverWebSocketTests.MissingPath_Ws_IsRefusedWith404")]
    public Task MissingPath_Ws_IsRefusedWith404() =>
        JudgeAsync(Refused(Case(Named(), "ws", ["{surl}/missing"], [], ServedFiles, IsRefused("404"))));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverWebSocketTests.Directory_WithoutListDirectories_IsRefusedWith404")]
    public Task Directory_WithoutListDirectories_IsRefusedWith404() =>
        JudgeAsync(Refused(Case(Named(), "ws", ["{surl}/sub/"], [], ServedFiles, IsRefused("404"))));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverWebSocketTests.Directory_ListDirectories_ExitsZeroWithTheListing")]
    public Task Directory_ListDirectories_ExitsZeroWithTheListing() =>
        JudgeAsync(Case(Named(), "ws", ["{surl}/sub/"], ["--list-directories"], ServedFiles, ExitsZeroWriting("a.txt\n"u8.ToArray())));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverWebSocketTests.Head_Ws_IsRefusedWith405")]
    public Task Head_Ws_IsRefusedWith405() =>
        JudgeAsync(Refused(Case(Named(), "ws", ["-I", "{surl}/file.bin"], [], ServedFiles, IsRefused("405"))));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverWebSocketTests.NoCredentials_Account_IsRefusedWith401")]
    public async Task NoCredentials_Account_IsRefusedWith401()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await JudgeAsync(Refused(Case(Named(), "ws", ["{surl}/file.bin"], ["--user-file", accounts.Path], ServedFiles, IsRefused("401"))));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverWebSocketTests.BasicOverWs_Account_IsRefusedWith403")]
    public async Task BasicOverWs_Account_IsRefusedWith403()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await JudgeAsync(Refused(Case(
            Named(), "ws", ["-u", Credentials, "{surl}/file.bin"], ["--user-file", accounts.Path], ServedFiles, IsRefused("403"))));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverWebSocketTests.BasicOverWs_AllowPlaintextAuth_ExitsZeroWithTheFilesBytes")]
    public async Task BasicOverWs_AllowPlaintextAuth_ExitsZeroWithTheFilesBytes()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await JudgeAsync(Case(
            Named(),
            "ws",
            ["-u", Credentials, "{surl}/file.bin"],
            ["--allow-plaintext-auth", "--user-file", accounts.Path],
            ServedFiles,
            ExitsZeroWriting(FileBin)));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverWebSocketTests.BasicOverWss_Account_ExitsZeroWithTheFilesBytes")]
    public async Task BasicOverWss_Account_ExitsZeroWithTheFilesBytes()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await JudgeAsync(Case(
            Named(), "wss", ["-k", "-u", Credentials, "{surl}/file.bin"], ["--self-signed", "--user-file", accounts.Path], ServedFiles, ExitsZeroWriting(FileBin)));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverWebSocketTests.BasicOverWss_WrongPassword_IsRefusedWith401")]
    public async Task BasicOverWss_WrongPassword_IsRefusedWith401()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await JudgeAsync(Refused(Case(
            Named(),
            "wss",
            ["-k", "-u", $"{AccountsFile.User}:wrong", "{surl}/file.bin"],
            ["--self-signed", "--user-file", accounts.Path],
            ServedFiles,
            IsRefused("401"))));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverWebSocketTests.BearerOverWss_Account_ExitsZeroWithTheFilesBytes")]
    public async Task BearerOverWss_Account_ExitsZeroWithTheFilesBytes()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await JudgeAsync(Case(
            Named(),
            "wss",
            ["-k", "--oauth2-bearer", AccountsFile.BearerToken, "{surl}/file.bin"],
            ["--self-signed", "--user-file", accounts.Path],
            ServedFiles,
            ExitsZeroWriting(FileBin)));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverWebSocketTests.AwsSigV4OverWss_Account_ExitsZeroWithTheFilesBytes")]
    public async Task AwsSigV4OverWss_Account_ExitsZeroWithTheFilesBytes()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await JudgeAsync(Case(
            Named(),
            "wss",
            ["-k", "--aws-sigv4", "aws:amz:us-east-1:s3", "-u", Credentials, "{surl}/file.bin"],
            ["--self-signed", "--user-file", accounts.Path],
            ServedFiles,
            ExitsZeroWriting(FileBin)));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverWebSocketTests.DigestOverWs_Account_IsRefusedAtTheChallenge")]
    public async Task DigestOverWs_Account_IsRefusedAtTheChallenge()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await JudgeAsync(Refused(Case(
            Named(), "ws", ["--digest", "-u", Credentials, "{surl}/file.bin"], ["--user-file", accounts.Path], ServedFiles, IsRefused("401"))));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverWebSocketTests.NtlmOverWs_Account_IsRefusedAtTheType2Challenge")]
    public async Task NtlmOverWs_Account_IsRefusedAtTheType2Challenge()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await JudgeAsync(Refused(Case(
            Named(),
            "ws",
            ["--ntlm", "-u", Credentials, "{surl}/file.bin"],
            ["--auth", "basic,ntlm", "--allow-plaintext-auth", "--user-file", accounts.Path],
            ServedFiles,
            IsRefused("401"))));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverWebSocketTests.Echo_IdleTimeout_ExitsZeroWithSurlsGoingAwayClose")]
    public Task Echo_IdleTimeout_ExitsZeroWithSurlsGoingAwayClose() =>
        JudgeAsync(Case(Named(), "ws", ["{surl}/"], ["--ws-echo", "--idle-timeout", "2"], NoFiles, ExitsZeroWriting(GoingAway)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverWebSocketTests.Echo_MaxTime_ExitsZeroWithSurlsGoingAwayClose")]
    public Task Echo_MaxTime_ExitsZeroWithSurlsGoingAwayClose() =>
        JudgeAsync(Case(Named(), "ws", ["{surl}/"], ["--ws-echo", "--max-time", "2"], NoFiles, ExitsZeroWriting(GoingAway)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverWebSocketTests.Echo_CurlsMaxTimeFirst_ExitsOperationTimedOut")]
    public Task Echo_CurlsMaxTimeFirst_ExitsOperationTimedOut() =>
        JudgeAsync(Case(Named(), "ws", ["-m", "2", "{surl}/"], ["--ws-echo", "--idle-timeout", "30"], NoFiles, run =>
            run.ExitCode == OperationTimedOut ? null : Describe(run)));

    private static CurlPortMeasurementCase Case(
        (string Name, string Mirrors) test,
        string scheme,
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> surlOptions,
        IReadOnlyDictionary<string, byte[]> files,
        Func<CurlPortMeasuredRun, string?> check) =>
        new(test.Name, $"{Mirrored}.{test.Mirrors}", scheme, ["-sS", .. arguments], scheme, surlOptions, files, check);

    /// <summary>
    /// A refusal case compares the standard error line the mirrored test asserts on, <c>Refused WebSocket upgrade: </c>.
    /// </summary>
    private static CurlPortMeasurementCase Refused(CurlPortMeasurementCase measurementCase) =>
        measurementCase with { AssertedStandardErrorFragments = ["Refused WebSocket upgrade: "] };

    private static (string Name, string Mirrors) Named([CallerMemberName] string method = "") => (method, method);

    private static string Describe(CurlPortMeasuredRun run) =>
        $"exit {run.ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "timeout"}, {run.StandardOutput.Length} bytes out, stderr: {run.StandardError.Trim()}";

    private static Func<CurlPortMeasuredRun, string?> ExitsZeroWriting(byte[] expected) =>
        run => run.ExitCode == 0 && run.StandardOutput.AsSpan().SequenceEqual(expected) ? null : Describe(run);

    private static Func<CurlPortMeasuredRun, string?> IsRefused(string status) =>
        run => run.ExitCode == HttpReturnedError
            && run.StandardError.Contains($"Refused WebSocket upgrade: {status}", StringComparison.Ordinal)
            && run.StandardOutput.Length == 0
                ? null
                : Describe(run);

    private static byte[] MakeFileBin()
    {
        var bytes = new byte[200000];
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = (byte)(index * 7 % 251);
        }

        return bytes;
    }

    private async Task JudgeAsync(
        CurlPortMeasurementCase measurementCase, Func<SurlOnLoopback, CancellationToken, Task<IAsyncDisposable>>? arrangeAsync = null)
    {
        var measurement = await PinnedCurlPortBesideUpstreamCurl.MeasureAsync(TestContext, measurementCase, arrangeAsync);
        var listed = (await ListedFindingsAsync()).Contains(measurementCase.Name);
        var verdict = measurement.Verdict;

        Assert.AreNotEqual(CurlPortVerdictKind.SurlDefect, verdict.Kind, $"Surl defect: upstream's run is not what {measurementCase.Mirrors} asserts: {verdict.Reason}");
        if (verdict.Kind == CurlPortVerdictKind.PortDefect)
        {
            Assert.IsTrue(listed, $"A port defect {FindingsFile} does not list:\n{verdict}");
        }
        else if (verdict.Kind == CurlPortVerdictKind.Agreement)
        {
            Assert.IsFalse(listed, $"{FindingsFile} lists {measurementCase.Name}, but the port now agrees with upstream curl; remove the finding.");
        }
    }

    private async Task<HashSet<string>> ListedFindingsAsync()
    {
        var text = await File.ReadAllTextAsync(Path.Combine(PinnedUpstreamCurl.RepositoryRoot(), FindingsFile), TestContext.CancellationToken);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match row in Regex.Matches(text, @"^\| `([^`]+)` \|", RegexOptions.Multiline))
        {
            names.Add(row.Groups[1].Value);
        }

        return names;
    }

    private sealed class DisposeAction(Action action) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            action();
            return ValueTask.CompletedTask;
        }
    }
}
