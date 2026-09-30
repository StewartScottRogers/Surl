using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build downloads files and listings from a live, in-process
/// <c>surl --allow-anonymous ftp://</c>, each download case ADR-0052 decision 12 lists: passive
/// (<c>EPSV</c>, and <c>PASV</c> with <c>--disable-epsv</c>) and active (<c>-P -</c>, and
/// <c>PORT</c> with <c>--disable-eprt</c>) data connections, <c>-I</c>, <c>-r</c>, <c>-C</c>,
/// <c>-B</c>, each <c>--ftp-method</c>, an absent file, listings with and without
/// <c>--list-directories</c>, and <c>--max-connections</c>, with the exit code and output curl
/// gives. surl serves a fresh temporary directory holding <c>a.txt</c>, <c>dir/b.txt</c> and
/// <c>d1/d2/a.txt</c>. Inconclusive where no pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlFetchesFromSurlOverFtpTests
{
    // CURLE_FTP_COULDNT_RETR_FILE: the listing was refused with 550.
    private const int CouldNotRetrieveFile = 19;

    // CURLE_OPERATION_TIMEDOUT: surl answered 421 in place of the greeting.
    private const int OperationTimedOut = 28;

    // CURLE_REMOTE_FILE_NOT_FOUND: SIZE was answered 550.
    private const int RemoteFileNotFound = 78;

    private static readonly byte[] HelloWorld = "hello world\n"u8.ToArray();
    private static readonly Dictionary<string, byte[]> ServedFiles = new()
    {
        ["a.txt"] = HelloWorld,
        ["dir/b.txt"] = "bee\n"u8.ToArray(),
        ["d1/d2/a.txt"] = HelloWorld,
    };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("")]
    [DataRow("--disable-epsv")]
    [DataRow("-P -")]
    [DataRow("-P - --disable-eprt")]
    public async Task Download_EachDataConnectionMode_WritesTheFile(string options)
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync([.. Split(options), surl.UrlOf("a.txt")]);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("--ftp-method multicwd")]
    [DataRow("--ftp-method singlecwd")]
    [DataRow("--ftp-method nocwd")]
    public async Task Download_FileTwoDirectoriesDownByEachFtpMethod_WritesTheFile(string options)
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync([.. Split(options), surl.UrlOf("d1/d2/a.txt")]);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
    }

    [TestMethod]
    public async Task HeadersOnly_ServedFile_WritesLastModifiedAndContentLength()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync("-I", surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var headers = Encoding.ASCII.GetString(result.StandardOutput);
        StringAssert.Contains(headers, "Last-Modified: ");
        StringAssert.Contains(headers, "Content-Length: 12");
        StringAssert.Contains(headers, "Accept-ranges: bytes");
    }

    [TestMethod]
    [DataRow("0-4", "hello")]
    [DataRow("3-6", "lo w")]
    public async Task Download_Range_WritesThoseBytes(string range, string expected)
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync("-r", range, surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(expected, Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Download_ContinueAtOffset_WritesTheRest()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync("-C", "5", surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(" world\n", Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Download_UseAscii_ExitsZero()
    {
        await using var surl = await StartSurlAsync();

        // ADR-0052 row 39: surl sends the bytes unconverted, but the Windows tool writes -B output
        // in text mode, so the line end differs by platform and only the text is compared.
        var result = await RunCurlAsync("-B", surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual("hello world", Encoding.ASCII.GetString(result.StandardOutput).TrimEnd('\r', '\n'));
    }

    [TestMethod]
    public async Task Download_AbsentFile_ExitsRemoteFileNotFound()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync(surl.UrlOf("missing.txt"));

        Assert.AreEqual(RemoteFileNotFound, result.ExitCode, result.StandardError);
        Assert.AreEqual("curl: (78) The file does not exist", result.StandardError.Trim());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("-l")]
    public async Task List_ListingsOff_ExitsCouldNotRetrieveFile(string options)
    {
        await using var surl = await StartSurlAsync();

        var result = await RunCurlAsync([.. Split(options), surl.UrlOf("dir/")]);

        Assert.AreEqual(CouldNotRetrieveFile, result.ExitCode, result.StandardError);
        Assert.AreEqual("curl: (19) RETR response: 550", result.StandardError.Trim());
    }

    [TestMethod]
    public async Task List_ListingsOn_WritesTheLongListing()
    {
        await using var surl = await StartSurlAsync("--list-directories");

        var result = await RunCurlAsync(surl.UrlOf("dir/"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Matches(
            Encoding.ASCII.GetString(result.StandardOutput),
            new Regex(@"\A-rw-r--r-- 1 surl surl +4 [A-Z][a-z]{2} [ \d]\d +[\d:]+ b\.txt\r\n\z"));
    }

    [TestMethod]
    public async Task ListNames_ListingsOn_WritesEachName()
    {
        await using var surl = await StartSurlAsync("--list-directories");

        var result = await RunCurlAsync("-l", surl.UrlOf("d1/"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual("d2\r\n", Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task ListMachineReadable_ListingsOn_WritesEachEntrysFacts()
    {
        await using var surl = await StartSurlAsync("--list-directories");

        var result = await RunCurlAsync("-X", "MLSD", surl.UrlOf("dir/"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Matches(
            Encoding.ASCII.GetString(result.StandardOutput),
            new Regex(@"\Atype=file;size=4;modify=\d{14}; b\.txt\r\n\z"));
    }

    [TestMethod]
    public async Task Download_MaxConnections1WhileAnotherClientHoldsIt_ExitsOperationTimedOut()
    {
        await using var surl = await StartSurlAsync("--max-connections", "1");
        using var holder = new TcpClient();
        await holder.ConnectAsync(surl.BaseUrl.Host, surl.BaseUrl.Port, TestContext.CancellationToken);
        var greeting = new byte[4];
        await holder.GetStream().ReadExactlyAsync(greeting, TestContext.CancellationToken);
        Assert.AreEqual("220 ", Encoding.ASCII.GetString(greeting));

        var result = await RunCurlAsync(surl.UrlOf("a.txt"));

        Assert.AreEqual(OperationTimedOut, result.ExitCode, result.StandardError);
    }

    private static string[] Split(string options) => options.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private Task<SurlOnLoopback> StartSurlAsync(params string[] options) =>
        SurlOnLoopback.StartAsync(
            "ftp", ServedFiles, ["dir", "d1", "d1/d2"], ["--allow-anonymous", .. options], TestContext.CancellationToken);

    private Task<UpstreamCurlRunResult> RunCurlAsync(params string[] arguments) =>
        PinnedUpstreamCurl.RunAsync(TestContext, ["-sS", .. arguments]);
}
