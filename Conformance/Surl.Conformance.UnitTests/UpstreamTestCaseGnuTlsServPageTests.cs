using System.Text;

namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseGnuTlsServPageTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "UpstreamTestCases");

    // The head upstream curl 8.21.0 sends for test 320's command line (Host, then -A's agent).
    private static readonly KeyValuePair<string, string>[] Test320Fields =
    [
        new("Host", "127.0.0.1:41817"),
        new("User-Agent", "curl-test-suite"),
        new("Accept", "*/*"),
    ];

    [TestMethod]
    public void Compose_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseGnuTlsServPage.Compose(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseGnuTlsServPage.Compose("jsmith", null!));
    }

    [TestMethod]
    public void Compose_UserAndFields_WritesGnuTlsServsHeadThenEveryPageLineStartingWithALessThanSign()
    {
        var page = UpstreamTestCaseGnuTlsServPage.Compose("jsmith", Test320Fields);

        Assert.AreEqual(
            "HTTP/1.0 200 OK\r\nContent-type: text/html\r\n\r\n"
            + "<HTML><BODY>\n"
            + "<CENTER><H1>This is <a href=\"http://www.gnu.org/software/gnutls\">GnuTLS</a></H1></CENTER>\n"
            + "<p>Connected as user 'jsmith'.</p>\n"
            + "<hr><P>Your HTTP header was:<PRE>Host: 127.0.0.1:41817\r\nUser-Agent: curl-test-suite\r\nAccept: */*\r\n\r\n"
            + "</PRE></P>\n"
            + "</BODY></HTML>\n",
            Encoding.Latin1.GetString(page));
    }

    [TestMethod]
    public void Compose_Test320sRequest_PassesTest320sVerifySection()
    {
        var testCase = UpstreamTestCaseParser.Parse(File.ReadAllBytes(Path.Combine(Root, "data", "test320")), Test320Environment());
        var page = UpstreamTestCaseGnuTlsServPage.Compose("jsmith", Test320Fields);
        var files = new UnitTestInMemoryRunFiles().With("log/curl320.out", Encoding.Latin1.GetString(page));
        var run = new UpstreamTestCaseRunRecord(0, [], [], page, [], [], []);

        var result = new UpstreamTestCaseVerifyChecker(files, TlsSrpFeatures()).Check(testCase, run);

        Assert.IsTrue(result.Passed, result.ToString());
    }

    [TestMethod]
    public void Compose_AnotherUser_FailsTest320sVerifySection()
    {
        var testCase = UpstreamTestCaseParser.Parse(File.ReadAllBytes(Path.Combine(Root, "data", "test320")), Test320Environment());
        var page = UpstreamTestCaseGnuTlsServPage.Compose("alice", Test320Fields);
        var files = new UnitTestInMemoryRunFiles().With("log/curl320.out", Encoding.Latin1.GetString(page));
        var run = new UpstreamTestCaseRunRecord(0, [], [], page, [], [], []);

        var result = new UpstreamTestCaseVerifyChecker(files, TlsSrpFeatures()).Check(testCase, run);

        Assert.IsFalse(result.Passed);
    }

    private static HashSet<string> TlsSrpFeatures() => new(["TLS-SRP", "SSL"], StringComparer.Ordinal);

    private static UpstreamTestCaseEnvironment Test320Environment() => new(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HOSTIP"] = "127.0.0.1",
            ["HTTPTLSPORT"] = "41817",
            ["LOGDIR"] = "log",
            ["TESTNUMBER"] = "320",
            ["SRCDIR"] = Root.Replace('\\', '/'),
        },
        TlsSrpFeatures(),
        new ImportedFileReader(),
        TimeProvider.System);

    private sealed class ImportedFileReader : IUpstreamTestCaseFileReader
    {
        public byte[] ReadAllBytes(string path)
        {
            var resolved = Path.Combine(Root, path);
            return File.Exists(resolved) ? File.ReadAllBytes(resolved) : [];
        }
    }
}
