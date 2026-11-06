using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// Phase 7 for SMB and SMBS (ADR-0089, BL-587): every conversation
/// <see cref="UpstreamCurlTransfersFilesWithSurlOverSmbTests"/> runs, through the pinned upstream build
/// that supports SMB and the pinned Curl port, each against its own fresh <c>surl</c>, judged by
/// <see cref="PinnedCurlPortBesideUpstreamCurl"/>. One test per mirrored test, each naming it in its
/// <see cref="DescriptionAttribute"/>. A test fails when upstream's run is not what the mirrored test
/// asserts (a Surl defect), when the port disagrees and <c>Documentation/Conformance/CurlPort/Smb.md</c>
/// does not list the case, and when a listed case agrees (ADR-0089 decision 5). The relay records every
/// <c>smb://</c> connection; the <c>smbs://</c> cases compare no request bytes, which are TLS's
/// (decision 4). Inconclusive when either pinned binary is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortBesideUpstreamCurlOverSmbTests
{
    private const string FindingsFile = "Documentation/Conformance/CurlPort/Smb.md";

    private const string Mirrored = nameof(UpstreamCurlTransfersFilesWithSurlOverSmbTests);

    private const string Account = "alice:secret";

    private static readonly byte[] HelloSmb = "hello smb\n"u8.ToArray();
    private static readonly byte[] UploadText = "uploaded\n"u8.ToArray();
    private static readonly byte[] Big = [.. Enumerable.Range(0, 40000).Select(index => (byte)(index * 7 % 251))];
    private static readonly Dictionary<string, byte[]> ServedFiles = new()
    {
        ["files/hello.txt"] = HelloSmb,
        ["files/big.bin"] = Big,
        [".hidden/x.txt"] = "hidden\n"u8.ToArray(),
    };

    private static readonly string[] AccountOptions = ["--auth", "ntlmv1", "-u", Account];

    private IsolatedCurlHome localFiles = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateLocalFiles() => localFiles = new IsolatedCurlHome();

    [TestCleanup]
    public void DeleteLocalFiles() => localFiles.Dispose();

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSmbTests.Download_ServedFile_WritesItsBytes")]
    [DataRow("files/hello.txt")]
    [DataRow("files/big.bin")]
    public Task Download_ServedFile_WritesItsBytes(string path) =>
        JudgeAsync(Case(Named(path), ["-u", Account, "{surl}/" + path], AccountOptions, ExitsZeroWriting(ServedFiles[path])));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSmbTests.Download_UserWithDomain_LogsInAsTheUser")]
    [DataRow("WORKGROUP/alice:secret")]
    [DataRow("WORKGROUP\\alice:secret")]
    public Task Download_UserWithDomain_LogsInAsTheUser(string user) =>
        JudgeAsync(Case(Named(user), ["-u", user, "{surl}/files/hello.txt"], AccountOptions, ExitsZeroWriting(HelloSmb)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSmbTests.Download_WrongPasswordOrUserNameCase_Exits67")]
    [DataRow("alice:wrong")]
    [DataRow("ALICE:secret")]
    public Task Download_WrongPasswordOrUserNameCase_Exits67(string user) =>
        JudgeAsync(Case(Named(user), ["-u", user, "{surl}/files/hello.txt"], AccountOptions, Exits(67)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSmbTests.Download_NoUser_CurlItselfExits67")]
    public Task Download_NoUser_CurlItselfExits67() =>
        JudgeAsync(Case(Named(), ["{surl}/files/hello.txt"], AccountOptions, Exits(67)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSmbTests.Download_NoFileAfterTheShare_CurlItselfExits3")]
    public Task Download_NoFileAfterTheShare_CurlItselfExits3() =>
        JudgeAsync(Case(Named(), ["-u", Account, "{surl}/files"], AccountOptions, Exits(3)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSmbTests.Download_MissingShareOrFileHiddenShareOrDirectory_Exits78")]
    [DataRow("nosuch/hello.txt")]
    [DataRow(".hidden/x.txt")]
    [DataRow("files/missing.txt")]
    [DataRow("files/sub")]
    [DataRow("files/")]
    public Task Download_MissingShareOrFileHiddenShareOrDirectory_Exits78(string path) =>
        JudgeAsync(Case(Named(path), ["-u", Account, "{surl}/" + path], AccountOptions, Exits(78)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSmbTests.Download_DotDotPathAsIs_Exits78")]
    public Task Download_DotDotPathAsIs_Exits78() =>
        JudgeAsync(Case(Named(), ["-u", Account, "--path-as-is", "{surl}/files/../files/hello.txt"], AccountOptions, Exits(78)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSmbTests.Upload_UploadsOff_Exits9AndStoresNothing")]
    public async Task Upload_UploadsOff_Exits9AndStoresNothing() =>
        await JudgeAsync(Case(Named(), ["-u", Account, "-T", await WriteUploadAsync("up.txt", UploadText), "{surl}/files/up.txt"], AccountOptions, run =>
            Exits(9)(run) ?? Absent(run, "files/up.txt")) with
        {
            ComparedStoredFiles = ["files/up.txt"],
        });

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSmbTests.Upload_UploadsOn_StoresTheBytes")]
    [DataRow("up.txt", "files/up.txt")]
    [DataRow("big.bin", "files/big2.bin")]
    public async Task Upload_UploadsOn_StoresTheBytes(string name, string path)
    {
        var contents = name == "big.bin" ? Big : UploadText;
        var upload = await WriteUploadAsync(name, contents);

        await JudgeAsync(Case(Named($"{name}, {path}"), ["-u", Account, "-T", upload, "{surl}/" + path], [.. AccountOptions, "--allow-uploads"], run =>
            Exits(0)(run) ?? Stored(run, path, contents)) with
        {
            ComparedStoredFiles = [path],
        });
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSmbTests.Upload_MissingShare_Exits78")]
    public async Task Upload_MissingShare_Exits78() =>
        await JudgeAsync(Case(Named(), ["-u", Account, "-T", await WriteUploadAsync("up.txt", UploadText), "{surl}/nodir/up.txt"], [.. AccountOptions, "--allow-uploads"], Exits(78)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSmbTests.Upload_PastMaxFilesize_Exits25AndLeavesNoFile")]
    public async Task Upload_PastMaxFilesize_Exits25AndLeavesNoFile() =>
        await JudgeAsync(Case(
            Named(),
            ["-u", Account, "-T", await WriteUploadAsync("big.bin", Big), "{surl}/files/big3.bin"],
            [.. AccountOptions, "--allow-uploads", "--max-filesize", "1000"],
            run => Exits(25)(run) ?? Absent(run, "files/big3.bin")) with
        {
            ComparedStoredFiles = ["files/big3.bin"],
        });

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSmbTests.Download_NoAccountsOrNtlmV1NotAccepted_Exits67")]
    [DataRow("--auth", "ntlmv1")]
    [DataRow("-u", Account)]
    public Task Download_NoAccountsOrNtlmV1NotAccepted_Exits67(string option, string value) =>
        JudgeAsync(Case(Named($"{option}, {value}"), ["-u", Account, "{surl}/files/hello.txt"], [option, value], Exits(67)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSmbTests.Download_AllowAnonymous_LetsAnyLoginIn")]
    public Task Download_AllowAnonymous_LetsAnyLoginIn() =>
        JudgeAsync(Case(Named(), ["-u", "anyone:anything", "{surl}/files/hello.txt"], ["--allow-anonymous"], ExitsZeroWriting(HelloSmb)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSmbTests.Smbs_Insecure_WritesTheFile")]
    public Task Smbs_Insecure_WritesTheFile() =>
        JudgeAsync(Case(Named(), ["-k", "-u", Account, "{surl}/files/hello.txt"], [.. AccountOptions, "--self-signed"], ExitsZeroWriting(HelloSmb), "smbs"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSmbTests.Smbs_SelfSignedCertificateNotTrusted_Exits60")]
    public Task Smbs_SelfSignedCertificateNotTrusted_Exits60() =>
        JudgeAsync(Case(Named(), ["-u", Account, "{surl}/files/hello.txt"], [.. AccountOptions, "--self-signed"], Exits(60), "smbs"));

    // --- The case, the mirrored assertions and the judgement ---

    /// <summary>
    /// A case as the mirrored test runs it: <c>-sS -m 20</c> before its arguments, against the
    /// <c>surl</c> <paramref name="scheme"/> listener, with the build <c>PinnedUpstreamCurl</c> picks for
    /// <c>smb</c> on the upstream side.
    /// </summary>
    private static CurlPortMeasurementCase Case(
        (string Name, string Mirrors) test,
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> surlOptions,
        Func<CurlPortMeasuredRun, string?> check,
        string scheme = "smb") =>
        new(test.Name, $"{Mirrored}.{test.Mirrors}", "smb", ["-sS", "-m", "20", .. arguments], scheme, surlOptions, ServedFiles, check);

    /// <summary>
    /// The case's name, the first column of the findings file: the test method's name, followed by
    /// its data row in brackets when it has one; and the mirrored method, which has the same name.
    /// </summary>
    private static (string Name, string Mirrors) Named(string? dataRow = null, [CallerMemberName] string method = "") =>
        (dataRow is null ? method : $"{method}[{dataRow}]", method);

    private static string Describe(CurlPortMeasuredRun run) =>
        $"exit {run.ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "timeout"}, {run.StandardOutput.Length} bytes out, stderr: {run.StandardError.Trim()}";

    private static Func<CurlPortMeasuredRun, string?> Exits(int exitCode) =>
        run => run.ExitCode == exitCode ? null : Describe(run);

    private static Func<CurlPortMeasuredRun, string?> ExitsZeroWriting(byte[] expected) =>
        run => run.ExitCode == 0 && run.StandardOutput.AsSpan().SequenceEqual(expected) ? null : Describe(run);

    private static string? Absent(CurlPortMeasuredRun run, string name) =>
        run.Surl.StoredFiles.ContainsKey(name) ? $"{name} is stored" : null;

    private static string? Stored(CurlPortMeasuredRun run, string name, byte[] expected) =>
        run.Surl.StoredFiles.TryGetValue(name, out var stored) && stored.AsSpan().SequenceEqual(expected)
            ? null
            : $"{name} stored as {(stored is null ? "nothing" : $"{stored.Length} other bytes")}";

    private Task<string> WriteUploadAsync(string name, byte[] contents) => localFiles.WriteFileAsync(name, contents, TestContext.CancellationToken);

    private async Task JudgeAsync(CurlPortMeasurementCase measurementCase)
    {
        var measurement = await PinnedCurlPortBesideUpstreamCurl.MeasureAsync(TestContext, measurementCase, CreateEmptySubdirectoryAsync);
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

    /// <summary>
    /// Adds the empty <c>files/sub</c> folder the mirrored test serves, which no served file implies.
    /// </summary>
    private static Task<IAsyncDisposable> CreateEmptySubdirectoryAsync(SurlOnLoopback surl, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.Combine(surl.ServedDirectory!, "files", "sub"));
        return Task.FromResult<IAsyncDisposable>(new NothingToDispose());
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

    private sealed class NothingToDispose : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
