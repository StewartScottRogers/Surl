using System.Text;
using static Surl.Conformance.UpstreamTestCaseText;

namespace Surl.Conformance;

/// <summary>
/// The Curl port as an upstream test case's client through <see cref="IUpstreamTestCaseClient"/>
/// (ADR-0089 decision 1(b), amendment 7), with no process: what the port is started with for a case,
/// how its run is checked against the case's verify section, and that upstream's locator still
/// refuses the port's binary (ADR-0083 decision 2).
/// </summary>
[TestClass]
public sealed class CurlPortTestCaseClientTests
{
    private const string PortPath = "/curl-port/abc123/curl";
    private const string CaseDirectory = "/work/port-test5";

    private static readonly byte[] PortBytes = [0x4D, 0x5A, 0x10];

    private static readonly PinnedCurlPortClient Port = CurlPortClientLocatorTests.Pin(PortPath, PortBytes);

    [TestMethod]
    public void ExecutablePathAndDescription_LocatedPort_NameThePortsVerifiedPathAndPin()
    {
        var client = new CurlPortTestCaseClient(Runner());

        Assert.AreEqual(PortPath, client.ExecutablePath);
        Assert.AreEqual($"Curl port client abc123 ({Port.Sha256})", client.Description);
    }

    [TestMethod]
    public void PlayCase_PortRunnerThroughTheSeam_StartsThePortWithTheCasesCommandLineInItsFolder()
    {
        var runner = Runner();
        IUpstreamTestCaseClient client = new CurlPortTestCaseClient(runner);
        var testCase = UpstreamTestCaseParser.Parse(Bytes(CaseSource), Environment(Variables(client)));
        var commandLine = UpstreamTestCaseCommandLine.Build(5, testCase, "log");
        var environment = new Dictionary<string, string> { ["HOME"] = CaseDirectory };

        var startInfo = runner.CreateStartInfo(commandLine.Arguments, environment, CaseDirectory);

        Assert.AreEqual(PortPath, startInfo.FileName);
        Assert.AreEqual(CaseDirectory, startInfo.WorkingDirectory);
        CollectionAssert.AreEqual(commandLine.Arguments.ToArray(), startInfo.ArgumentList.ToArray());
        CollectionAssert.Contains(startInfo.ArgumentList.ToArray(), "http://127.0.0.1:8990/5");
        Assert.AreEqual(CaseDirectory, startInfo.Environment["HOME"]);
    }

    [TestMethod]
    public void PlayCase_PortRunMatchingTheVerifySection_Passes()
    {
        var testCase = UpstreamTestCaseParser.Parse(Bytes(CaseSource), Environment(Variables(new CurlPortTestCaseClient(Runner()))));
        var portRun = UpstreamTestCaseClientRun.From(CurlPortClientRunResult.Exited(Port, 0, Bytes("hello\n"), string.Empty));

        var record = UpstreamTestCaseRunRecord.FromRun(portRun, Bytes("hello\n"), Bytes("GET /5 HTTP/1.1\r\nUser-Agent: curl/8.22.0\r\n\r\n"), []);
        var verdict = new UpstreamTestCaseVerifyChecker(new UnitTestInMemoryRunFiles(), new HashSet<string>()).Check(testCase, record);

        Assert.IsTrue(verdict.Passed, verdict.ToString());
    }

    [TestMethod]
    public void PlayCase_PortRunSendingUpstreamsVersion_FailsTheProtocolSection()
    {
        var testCase = UpstreamTestCaseParser.Parse(Bytes(CaseSource), Environment(Variables(new CurlPortTestCaseClient(Runner()))));
        var portRun = UpstreamTestCaseClientRun.From(CurlPortClientRunResult.Exited(Port, 0, Bytes("hello\n"), string.Empty));

        var record = UpstreamTestCaseRunRecord.FromRun(portRun, Bytes("hello\n"), Bytes("GET /5 HTTP/1.1\r\nUser-Agent: curl/8.21.0\r\n\r\n"), []);
        var verdict = new UpstreamTestCaseVerifyChecker(new UnitTestInMemoryRunFiles(), new HashSet<string>()).Check(testCase, record);

        Assert.AreEqual("protocol", verdict.FailedSection);
    }

    [TestMethod]
    public void RequirePinned_PortsBinary_UpstreamLocatorStillRefusesIt()
    {
        var upstreamPins = UpstreamTestCaseSelectorTests.ReferencePins();
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile(PortPath, PortBytes));

        var exception = Assert.ThrowsExactly<UnpinnedUpstreamCurlException>(() => locator.RequirePinned(upstreamPins, PortPath));

        Assert.AreEqual(Port.Sha256, exception.Sha256);
        StringAssert.Contains(exception.Message, "the Curl port is never one");
    }

    [TestMethod]
    public void Constructor_NullRunner_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlPortTestCaseClient(null!));
    }

    // A case as runtests would give the port: its own %VERSION, from its own --version.
    private const string CaseSource =
        "<testcase>\n<reply>\n<data>\nhello\n</data>\n</reply>\n" +
        "<client>\n<server>\nhttp\n</server>\n<command>\nhttp://%HOSTIP:%HTTPPORT/%TESTNUMBER\n</command>\n</client>\n" +
        "<verify>\n<stdout>\nhello\n</stdout>\n<protocol crlf=\"yes\">\nGET /%TESTNUMBER HTTP/1.1\nUser-Agent: curl/%VERSION\n\n</protocol>\n</verify>\n</testcase>\n";

    private static Dictionary<string, string> Variables(IUpstreamTestCaseClient client) => new(StringComparer.Ordinal)
    {
        ["HOSTIP"] = "127.0.0.1",
        ["HTTPPORT"] = "8990",
        ["TESTNUMBER"] = "5",
        ["CURL"] = client.ExecutablePath,
        ["VERSION"] = CurlVersionReport.Parse(CurlVersionReportTests.PortVersionOutput).Version,
    };

    private static CurlPortClientRunner Runner() =>
        new(CurlPortClientLocation.Found(Port, PortPath), TimeSpan.FromSeconds(1), TimeProvider.System);
}
