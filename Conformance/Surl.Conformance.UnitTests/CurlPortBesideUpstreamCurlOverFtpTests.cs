using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// Phase 7 for FTP and FTPS (ADR-0089, BL-581): every conversation
/// <see cref="UpstreamCurlFetchesFromSurlOverFtpTests"/>, <see cref="UpstreamCurlLogsInToSurlOverFtpTests"/>
/// and <see cref="UpstreamCurlUploadsToSurlOverFtpTests"/> run, through pinned upstream curl and the
/// pinned Curl port, each against its own fresh <c>surl</c>, judged by
/// <see cref="PinnedCurlPortBesideUpstreamCurl"/>. One test per mirrored test, each naming it in its
/// <see cref="DescriptionAttribute"/>. A test fails when upstream's run is not what the mirrored test
/// asserts (a Surl defect), when the port disagrees and <c>Documentation/Conformance/CurlPort/Ftp.md</c>
/// does not list the case, and when a listed case agrees (ADR-0089 decision 5). The relay records the
/// control connection only; data connections go straight to the port <c>surl</c> announces (decision 4).
/// Inconclusive when either pinned binary is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortBesideUpstreamCurlOverFtpTests
{
    private const string FindingsFile = "Documentation/Conformance/CurlPort/Ftp.md";

    private const string Fetches = nameof(UpstreamCurlFetchesFromSurlOverFtpTests);
    private const string LogsIn = nameof(UpstreamCurlLogsInToSurlOverFtpTests);
    private const string Uploads = nameof(UpstreamCurlUploadsToSurlOverFtpTests);

    private const int CouldNotRetrieveFile = 19;
    private const int QuoteError = 21;
    private const int UploadFailed = 25;
    private const int OperationTimedOut = 28;
    private const int UseSslFailed = 64;
    private const int LoginDenied = 67;
    private const int RemoteDiskFull = 70;
    private const int RemoteFileNotFound = 78;

    private static readonly byte[] HelloWorld = "hello world\n"u8.ToArray();
    private static readonly byte[] HelloAgain = "hello again"u8.ToArray();
    private static readonly DateTime ServedModificationTime = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static readonly string Account = $"{AccountsFile.User}:{AccountsFile.Password}";

    private static readonly Dictionary<string, byte[]> FetchedFiles = new()
    {
        ["a.txt"] = HelloWorld,
        ["dir/b.txt"] = "bee\n"u8.ToArray(),
        ["d1/d2/a.txt"] = HelloWorld,
    };

    private static readonly Dictionary<string, byte[]> LoginFiles = new() { ["a.txt"] = HelloWorld };

    private static readonly Dictionary<string, byte[]> UploadFiles = new()
    {
        ["a.txt"] = HelloWorld,
        ["c.txt"] = "hello"u8.ToArray(),
    };

    private IsolatedCurlHome localFiles = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateLocalFiles() => localFiles = new IsolatedCurlHome();

    [TestCleanup]
    public void DeleteLocalFiles() => localFiles.Dispose();

    // --- UpstreamCurlFetchesFromSurlOverFtpTests ---

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverFtpTests.Download_EachDataConnectionMode_WritesTheFile")]
    [DataRow("")]
    [DataRow("--disable-epsv")]
    [DataRow("-P -")]
    [DataRow("-P - --disable-eprt")]
    public Task Download_EachDataConnectionMode_WritesTheFile(string options) =>
        JudgeAsync(Fetch(Named(options), [.. Split(options), "{surl}/a.txt"], [], ExitsZeroWriting(HelloWorld)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverFtpTests.Download_FileTwoDirectoriesDownByEachFtpMethod_WritesTheFile")]
    [DataRow("")]
    [DataRow("--ftp-method multicwd")]
    [DataRow("--ftp-method singlecwd")]
    [DataRow("--ftp-method nocwd")]
    public Task Download_FileTwoDirectoriesDownByEachFtpMethod_WritesTheFile(string options) =>
        JudgeAsync(Fetch(Named(options), [.. Split(options), "{surl}/d1/d2/a.txt"], [], ExitsZeroWriting(HelloWorld)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverFtpTests.HeadersOnly_ServedFile_WritesLastModifiedAndContentLength")]
    public Task HeadersOnly_ServedFile_WritesLastModifiedAndContentLength() =>
        JudgeAsync(Fetch(Named(), ["-I", "{surl}/a.txt"], [], run =>
        {
            var headers = Encoding.ASCII.GetString(run.StandardOutput);
            return run.ExitCode == 0
                && headers.Contains("Last-Modified: ", StringComparison.Ordinal)
                && headers.Contains("Content-Length: 12", StringComparison.Ordinal)
                && headers.Contains("Accept-ranges: bytes", StringComparison.Ordinal)
                ? null
                : Describe(run);
        }));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverFtpTests.Download_Range_WritesThoseBytes")]
    [DataRow("0-4", "hello")]
    [DataRow("3-6", "lo w")]
    public Task Download_Range_WritesThoseBytes(string range, string expected) =>
        JudgeAsync(Fetch(Named(range), ["-r", range, "{surl}/a.txt"], [], ExitsZeroWriting(Encoding.ASCII.GetBytes(expected))));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverFtpTests.Download_ContinueAtOffset_WritesTheRest")]
    public Task Download_ContinueAtOffset_WritesTheRest() =>
        JudgeAsync(Fetch(Named(), ["-C", "5", "{surl}/a.txt"], [], ExitsZeroWriting(" world\n"u8.ToArray())));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverFtpTests.Download_UseAscii_ExitsZero")]
    public Task Download_UseAscii_ExitsZero() =>
        JudgeAsync(Fetch(Named(), ["-B", "{surl}/a.txt"], [], run =>
            run.ExitCode == 0 && Encoding.ASCII.GetString(run.StandardOutput).TrimEnd('\r', '\n') == "hello world"
                ? null
                : Describe(run)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverFtpTests.Download_AbsentFile_ExitsRemoteFileNotFound")]
    public Task Download_AbsentFile_ExitsRemoteFileNotFound() =>
        JudgeAsync(Fetch(Named(), ["{surl}/missing.txt"], [], ExitsWithError(RemoteFileNotFound, "curl: (78) The file does not exist")) with
        {
            AssertedStandardErrorFragments = ["curl: ("],
        });

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverFtpTests.List_ListingsOff_ExitsCouldNotRetrieveFile")]
    [DataRow("")]
    [DataRow("-l")]
    public Task List_ListingsOff_ExitsCouldNotRetrieveFile(string options) =>
        JudgeAsync(Fetch(Named(options), [.. Split(options), "{surl}/dir/"], [], ExitsWithError(CouldNotRetrieveFile, "curl: (19) RETR response: 550")) with
        {
            AssertedStandardErrorFragments = ["curl: ("],
        });

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverFtpTests.List_ListingsOnOnWindows_WritesTheLongListingWithCrlf")]
    public Task List_ListingsOnOnWindows_WritesTheLongListingWithCrlf() =>
        JudgeAsync(Fetch(Named(), ["{surl}/dir/"], ["--list-directories"], ExitsZeroWritingMatch(
            @"\A-rw-r--r-- 1 surl surl +4 [A-Z][a-z]{2} [ \d]\d +[\d:]+ b\.txt\r\n\z")));

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverFtpTests.ListNames_ListingsOnOnWindows_WritesEachNameWithCrlf")]
    public Task ListNames_ListingsOnOnWindows_WritesEachNameWithCrlf() =>
        JudgeAsync(Fetch(Named(), ["-l", "{surl}/d1/"], ["--list-directories"], ExitsZeroWriting("d2\r\n"u8.ToArray())));

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverFtpTests.ListMachineReadable_ListingsOnOnWindows_WritesEachEntrysFactsWithCrlf")]
    public Task ListMachineReadable_ListingsOnOnWindows_WritesEachEntrysFactsWithCrlf() =>
        JudgeAsync(Fetch(Named(), ["-X", "MLSD", "{surl}/dir/"], ["--list-directories"], ExitsZeroWritingMatch(
            @"\Atype=file;size=4;modify=\d{14}; b\.txt\r\n\z")));

    // ADR-0089 decision 8: no port pin exists off Windows yet, so these are Inconclusive there today.
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverFtpTests.List_ListingsOnOffWindows_WritesTheLongListingWithLf")]
    public Task List_ListingsOnOffWindows_WritesTheLongListingWithLf() =>
        JudgeAsync(Fetch(Named(), ["{surl}/dir/"], ["--list-directories"], ExitsZeroWritingMatch(
            @"\A-rw-r--r-- 1 surl surl +4 [A-Z][a-z]{2} [ \d]\d +[\d:]+ b\.txt\n\z")));

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverFtpTests.ListNames_ListingsOnOffWindows_WritesEachNameWithLf")]
    public Task ListNames_ListingsOnOffWindows_WritesEachNameWithLf() =>
        JudgeAsync(Fetch(Named(), ["-l", "{surl}/d1/"], ["--list-directories"], ExitsZeroWriting("d2\n"u8.ToArray())));

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverFtpTests.ListMachineReadable_ListingsOnOffWindows_WritesEachEntrysFactsWithLf")]
    public Task ListMachineReadable_ListingsOnOffWindows_WritesEachEntrysFactsWithLf() =>
        JudgeAsync(Fetch(Named(), ["-X", "MLSD", "{surl}/dir/"], ["--list-directories"], ExitsZeroWritingMatch(
            @"\Atype=file;size=4;modify=\d{14}; b\.txt\n\z")));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverFtpTests.Download_MaxConnections1WhileAnotherClientHoldsIt_ExitsOperationTimedOut")]
    public async Task Download_MaxConnections1WhileAnotherClientHoldsIt_ExitsOperationTimedOut()
    {
        var greetings = new List<string>();
        var measurementCase = Fetch(Named(), ["{surl}/a.txt"], ["--max-connections", "1"], run =>
            run.ExitCode == OperationTimedOut && greetings.Count > 0 && greetings[0] == "220 " ? null : $"{Describe(run)}; holder greeting: {string.Join(", ", greetings)}");

        await JudgeAsync(measurementCase, async (surl, cancellationToken) =>
        {
            var holder = new TcpClient();
            await holder.ConnectAsync(surl.BaseUrl.Host, surl.BaseUrl.Port, cancellationToken);
            var greeting = new byte[4];
            await holder.GetStream().ReadExactlyAsync(greeting, cancellationToken);
            greetings.Add(Encoding.ASCII.GetString(greeting));
            return new DisposeAction(() => holder.Dispose());
        });
    }

    // --- UpstreamCurlLogsInToSurlOverFtpTests ---

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverFtpTests.Download_AnonymousLoginWithAllowAnonymous_WritesTheFile")]
    public Task Download_AnonymousLoginWithAllowAnonymous_WritesTheFile() =>
        JudgeAsync(LogIn(Named(), "ftp", ["{surl}/a.txt"], ["--allow-anonymous"], ExitsZeroWriting(HelloWorld)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverFtpTests.Download_AnonymousLoginWithoutAllowAnonymous_ExitsLoginDenied")]
    public Task Download_AnonymousLoginWithoutAllowAnonymous_ExitsLoginDenied() =>
        JudgeAsync(LogIn(Named(), "ftp", ["{surl}/a.txt"], [], run =>
            ExitsWithError(LoginDenied, "curl: (67) Access denied: 530")(run) ?? (run.StandardOutput.Length == 0 ? null : Describe(run))) with
        {
            AssertedStandardErrorFragments = ["curl: ("],
        });

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverFtpTests.Download_AccountOverPlaintextWithoutAllowPlaintextAuth_ExitsLoginDenied")]
    public Task Download_AccountOverPlaintextWithoutAllowPlaintextAuth_ExitsLoginDenied() =>
        JudgeAsync(LogIn(Named(), "ftp", ["-u", Account, "{surl}/a.txt"], ["--user", Account], ExitsWithError(LoginDenied, "curl: (67) Access denied: 530")) with
        {
            AssertedStandardErrorFragments = ["curl: ("],
        });

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverFtpTests.Download_AccountOverPlaintextWithAllowPlaintextAuth_WritesTheFile")]
    public Task Download_AccountOverPlaintextWithAllowPlaintextAuth_WritesTheFile() =>
        JudgeAsync(LogIn(Named(), "ftp", ["-u", Account, "{surl}/a.txt"], ["--user", Account, "--allow-plaintext-auth"], ExitsZeroWriting(HelloWorld)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverFtpTests.Download_WrongPasswordWithAllowPlaintextAuth_ExitsLoginDenied")]
    public Task Download_WrongPasswordWithAllowPlaintextAuth_ExitsLoginDenied() =>
        JudgeAsync(LogIn(
            Named(),
            "ftp",
            ["-u", $"{AccountsFile.User}:wrong", "{surl}/a.txt"],
            ["--user", Account, "--allow-plaintext-auth"],
            ExitsWithError(LoginDenied, "curl: (67) Access denied: 530")) with
        {
            AssertedStandardErrorFragments = ["curl: ("],
        });

    // The conversation turns to TLS after AUTH TLS, so the relay's bytes past it would be the TLS
    // library's, not curl's: no bytes are compared (CurlPortMeasurementCase.RecordsClientBytes).
    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverFtpTests.Download_AccountOverExplicitTls_WritesTheFile")]
    [DataRow("--ssl-reqd")]
    [DataRow("--ftp-ssl-control")]
    [DataRow("--ssl-reqd --ftp-ssl-ccc")]
    public Task Download_AccountOverExplicitTls_WritesTheFile(string tlsOptions) =>
        JudgeAsync(LogIn(
            Named(tlsOptions),
            "ftp",
            ["-k", .. Split(tlsOptions), "-u", Account, "{surl}/a.txt"],
            ["--user", Account, "--self-signed"],
            ExitsZeroWriting(HelloWorld)) with
        {
            RecordsClientBytes = false,
        });

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverFtpTests.Download_AccountOverImplicitFtps_WritesTheFile")]
    public Task Download_AccountOverImplicitFtps_WritesTheFile() =>
        JudgeAsync(LogIn(Named(), "ftps", ["-k", "-u", Account, "{surl}/a.txt"], ["--user", Account, "--self-signed"], ExitsZeroWriting(HelloWorld)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverFtpTests.Download_SslReqdWithNoCertificate_ExitsUseSslFailed")]
    public Task Download_SslReqdWithNoCertificate_ExitsUseSslFailed() =>
        JudgeAsync(LogIn(Named(), "ftp", ["--ssl-reqd", "-u", Account, "{surl}/a.txt"], ["--user", Account], ExitsWithError(UseSslFailed, "curl: (64) Requested SSL level failed")) with
        {
            AssertedStandardErrorFragments = ["curl: ("],
        });

    // --- UpstreamCurlUploadsToSurlOverFtpTests ---

    [TestMethod]
    [Description("Mirrors UpstreamCurlUploadsToSurlOverFtpTests.Upload_UploadsOff_ExitsUploadFailedAndWritesNothing")]
    public async Task Upload_UploadsOff_ExitsUploadFailedAndWritesNothing() =>
        await JudgeAsync(Upload(Named(), ["-T", await WriteUploadAsync(), "{surl}/b.txt"], [], ["b.txt"], run =>
            ExitsWithError(UploadFailed, "curl: (25) Failed FTP upload: 550")(run) ?? Absent(run, "b.txt")) with
        {
            AssertedStandardErrorFragments = ["curl: ("],
        });

    [TestMethod]
    [Description("Mirrors UpstreamCurlUploadsToSurlOverFtpTests.Upload_UploadsOn_WritesTheFile")]
    [DataRow("")]
    [DataRow("-P -")]
    public async Task Upload_UploadsOn_WritesTheFile(string options) =>
        await JudgeAsync(Upload(Named(options), [.. Split(options), "-T", await WriteUploadAsync(), "{surl}/b.txt"], ["--allow-uploads"], ["b.txt"], run =>
            ExitsZero(run) ?? Stored(run, "b.txt", HelloAgain)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlUploadsToSurlOverFtpTests.Append_ExistingFile_AppendsTheUpload")]
    public async Task Append_ExistingFile_AppendsTheUpload() =>
        await JudgeAsync(Upload(Named(), ["-a", "-T", await WriteUploadAsync(), "{surl}/a.txt"], ["--allow-uploads"], ["a.txt"], run =>
            ExitsZero(run) ?? Stored(run, "a.txt", [.. HelloWorld, .. HelloAgain])));

    [TestMethod]
    [Description("Mirrors UpstreamCurlUploadsToSurlOverFtpTests.ResumeUpload_PartialFile_SendsTheRest")]
    public async Task ResumeUpload_PartialFile_SendsTheRest() =>
        await JudgeAsync(Upload(Named(), ["-C", "-", "-T", await WriteUploadAsync(), "{surl}/c.txt"], ["--allow-uploads"], ["c.txt"], run =>
            ExitsZero(run) ?? Stored(run, "c.txt", HelloAgain)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlUploadsToSurlOverFtpTests.Upload_CreateDirs_CreatesEachDirectoryAndWritesTheFile")]
    public async Task Upload_CreateDirs_CreatesEachDirectoryAndWritesTheFile() =>
        await JudgeAsync(Upload(Named(), ["--ftp-create-dirs", "-T", await WriteUploadAsync(), "{surl}/new/deep/b.txt"], ["--allow-uploads"], ["new/deep/b.txt"], run =>
            ExitsZero(run) ?? Stored(run, "new/deep/b.txt", HelloAgain)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlUploadsToSurlOverFtpTests.Upload_PastMaxFilesize_ExitsRemoteDiskFullAndWritesNothing")]
    public async Task Upload_PastMaxFilesize_ExitsRemoteDiskFullAndWritesNothing() =>
        await JudgeAsync(Upload(Named(), ["-T", await WriteUploadAsync(), "{surl}/b.txt"], ["--allow-uploads", "--max-filesize", "4"], ["b.txt"], run =>
            (run.ExitCode == RemoteDiskFull ? null : Describe(run)) ?? Absent(run, "b.txt")));

    [TestMethod]
    [Description("Mirrors UpstreamCurlUploadsToSurlOverFtpTests.Quote_Delete_DeletesTheFile")]
    public Task Quote_Delete_DeletesTheFile() =>
        JudgeAsync(Upload(Named(), ["-Q", "DELE a.txt", "{surl}/"], ["--allow-uploads", "--list-directories"], ["a.txt"], run =>
            ExitsZero(run) ?? Absent(run, "a.txt")));

    [TestMethod]
    [Description("Mirrors UpstreamCurlUploadsToSurlOverFtpTests.Quote_Rename_RenamesTheFile")]
    public Task Quote_Rename_RenamesTheFile() =>
        JudgeAsync(Upload(Named(), ["-Q", "RNFR a.txt", "-Q", "RNTO renamed.txt", "{surl}/"], ["--allow-uploads", "--list-directories"], ["a.txt", "renamed.txt"], run =>
            ExitsZero(run) ?? Absent(run, "a.txt") ?? Stored(run, "renamed.txt", HelloWorld)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlUploadsToSurlOverFtpTests.Quote_MakeThenRemoveDirectoryAroundADownload_WritesTheFileAndLeavesNoDirectory")]
    public async Task Quote_MakeThenRemoveDirectoryAroundADownload_WritesTheFileAndLeavesNoDirectory()
    {
        var directoryLeft = new List<bool>();
        var measurementCase = Upload(Named(), ["-Q", "MKD x", "-Q", "-RMD x", "{surl}/a.txt"], ["--allow-uploads"], [], run =>
            ExitsZeroWriting(HelloWorld)(run) ?? (directoryLeft is [false, ..] ? null : $"directory x left behind: {string.Join(", ", directoryLeft)}"));

        await JudgeAsync(measurementCase, (surl, cancellationToken) =>
            Task.FromResult<IAsyncDisposable>(new DisposeAction(() => directoryLeft.Add(Directory.Exists(Path.Combine(surl.ServedDirectory!, "x"))))));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlUploadsToSurlOverFtpTests.Quote_DeleteWithUploadsOff_ExitsQuoteErrorAndKeepsTheFile")]
    public Task Quote_DeleteWithUploadsOff_ExitsQuoteErrorAndKeepsTheFile() =>
        JudgeAsync(Upload(Named(), ["-Q", "DELE a.txt", "{surl}/"], [], ["a.txt"], run =>
            ExitsWithError(QuoteError, "curl: (21) QUOT command failed with 550")(run) ?? Stored(run, "a.txt", HelloWorld)) with
        {
            AssertedStandardErrorFragments = ["curl: ("],
        });

    [TestMethod]
    [Description("Mirrors UpstreamCurlUploadsToSurlOverFtpTests.Quote_SiteChmod_ExitsQuoteError")]
    [DataRow("")]
    [DataRow("--allow-uploads")]
    public Task Quote_SiteChmod_ExitsQuoteError(string options) =>
        JudgeAsync(Upload(Named(options), ["-Q", "SITE CHMOD 644 a.txt", "{surl}/a.txt"], Split(options), [], run =>
            run.ExitCode == QuoteError ? null : Describe(run)));

    // --- The case, the mirrored assertions and the judgement ---

    private static CurlPortMeasurementCase Fetch(
        (string Name, string Mirrors) test, IReadOnlyList<string> arguments, IReadOnlyList<string> surlOptions, Func<CurlPortMeasuredRun, string?> check) =>
        Case(test, Fetches, "ftp", arguments, ["--allow-anonymous", .. surlOptions], FetchedFiles, check);

    private static CurlPortMeasurementCase LogIn(
        (string Name, string Mirrors) test, string scheme, IReadOnlyList<string> arguments, IReadOnlyList<string> surlOptions, Func<CurlPortMeasuredRun, string?> check) =>
        Case(test, LogsIn, scheme, arguments, surlOptions, LoginFiles, check);

    private static CurlPortMeasurementCase Upload(
        (string Name, string Mirrors) test,
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> surlOptions,
        IReadOnlyList<string> comparedStoredFiles,
        Func<CurlPortMeasuredRun, string?> check) =>
        Case(test, Uploads, "ftp", arguments, ["--allow-anonymous", .. surlOptions], UploadFiles, check) with
        {
            ComparedStoredFiles = comparedStoredFiles,
        };

    private static CurlPortMeasurementCase Case(
        (string Name, string Mirrors) test,
        string mirroredClass,
        string scheme,
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> surlOptions,
        IReadOnlyDictionary<string, byte[]> files,
        Func<CurlPortMeasuredRun, string?> check) =>
        new(test.Name, $"{mirroredClass}.{test.Mirrors}", scheme, ["-sS", .. arguments], scheme, surlOptions, files, check);

    /// <summary>
    /// The case's name, the first column of the findings file: the test method's name, followed by
    /// its data row in brackets when it has one; and the mirrored method, which has the same name.
    /// </summary>
    private static (string Name, string Mirrors) Named(string? dataRow = null, [CallerMemberName] string method = "") =>
        (dataRow is null ? method : $"{method}[{dataRow}]", method);

    private static string[] Split(string options) => options.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static string Describe(CurlPortMeasuredRun run) =>
        $"exit {run.ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "timeout"}, {run.StandardOutput.Length} bytes out, stderr: {run.StandardError.Trim()}";

    private static string? ExitsZero(CurlPortMeasuredRun run) => run.ExitCode == 0 ? null : Describe(run);

    private static Func<CurlPortMeasuredRun, string?> ExitsZeroWriting(byte[] expected) =>
        run => run.ExitCode == 0 && run.StandardOutput.AsSpan().SequenceEqual(expected) ? null : Describe(run);

    private static Func<CurlPortMeasuredRun, string?> ExitsZeroWritingMatch(string pattern) =>
        run => run.ExitCode == 0 && Regex.IsMatch(Encoding.ASCII.GetString(run.StandardOutput), pattern) ? null : Describe(run);

    private static Func<CurlPortMeasuredRun, string?> ExitsWithError(int exitCode, string standardError) =>
        run => run.ExitCode == exitCode && run.StandardError.Trim() == standardError ? null : Describe(run);

    private static string? Absent(CurlPortMeasuredRun run, string name) =>
        run.Surl.StoredFiles.ContainsKey(name) ? $"{name} is stored" : null;

    private static string? Stored(CurlPortMeasuredRun run, string name, byte[] expected) =>
        run.Surl.StoredFiles.TryGetValue(name, out var stored) && stored.AsSpan().SequenceEqual(expected)
            ? null
            : $"{name} stored as {(stored is null ? "nothing" : Convert.ToHexString(stored))}";

    private Task<string> WriteUploadAsync() => localFiles.WriteFileAsync("up.txt", HelloAgain, TestContext.CancellationToken);

    private async Task JudgeAsync(
        CurlPortMeasurementCase measurementCase, Func<SurlOnLoopback, CancellationToken, Task<IAsyncDisposable>>? arrangeAsync = null)
    {
        var measurement = await PinnedCurlPortBesideUpstreamCurl.MeasureAsync(
            TestContext,
            measurementCase,
            async (surl, cancellationToken) =>
            {
                PinModificationTimes(surl.ServedDirectory!);
                return arrangeAsync is null ? new DisposeAction(() => { }) : await arrangeAsync(surl, cancellationToken);
            });
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
    /// Gives every served file and folder one fixed modification time, so <c>-I</c>'s
    /// <c>Last-Modified</c>, the long listing's date and <c>MLSD</c>'s <c>modify</c> fact are the same
    /// in both runs, whose <c>surl</c>s wrote their content seconds apart.
    /// </summary>
    private static void PinModificationTimes(string servedDirectory)
    {
        foreach (var entry in new DirectoryInfo(servedDirectory).EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
        {
            entry.LastWriteTimeUtc = ServedModificationTime;
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
