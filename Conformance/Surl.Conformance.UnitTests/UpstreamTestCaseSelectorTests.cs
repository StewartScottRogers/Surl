namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseSelectorTests
{
    private const int Number = 42;

    [TestMethod]
    public void Constructor_NullRules_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new UpstreamTestCaseSelector(null!));
    }

    [TestMethod]
    public void FindSkipReason_NullCase_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Selector().FindSkipReason(Number, null!));
    }

    [TestMethod]
    public void FindSkipReason_PlainHttpCase_IsRunnable()
    {
        Assert.IsNull(Selector().FindSkipReason(Number, Case(client: Server("http"))));
    }

    [TestMethod]
    public void FindSkipReason_CaseListedInDisabled_SkipsDisabledUpstream()
    {
        var rules = Rules() with { DisabledCases = new HashSet<int> { Number } };

        Assert.AreEqual("disabled-upstream", new UpstreamTestCaseSelector(rules).FindSkipReason(Number, Case(client: Server("http"))));
    }

    [TestMethod]
    public void FindSkipReason_UnitTool_SkipsUnitTest()
    {
        Assert.AreEqual("unittest", Reason(Server("http") + "<tool>\nunit1300\n</tool>\n"));
    }

    [TestMethod]
    public void FindSkipReason_UnitTestFeature_SkipsUnitTest()
    {
        Assert.AreEqual("unittest", Reason(Server("http") + Features("unittest")));
    }

    [TestMethod]
    public void FindSkipReason_LibTestTool_SkipsLibTest()
    {
        Assert.AreEqual("libtest", Reason(Server("http") + "<tool>\nlib%TESTNUMBER\n</tool>\n"));
    }

    [TestMethod]
    public void FindSkipReason_DebugFeature_SkipsDebugBuild()
    {
        Assert.AreEqual("debug-build", Reason(Server("http") + Features("Debug")));
    }

    [TestMethod]
    public void FindSkipReason_TrackMemoryFeature_SkipsDebugBuild()
    {
        Assert.AreEqual("debug-build", Reason(Server("http") + Features("TrackMemory")));
    }

    [TestMethod]
    public void FindSkipReason_PerlCommand_SkipsPerlCommand()
    {
        Assert.AreEqual("perl-command", Reason(Server("http") + "<command type=\"perl\">\n-e 1\n</command>\n"));
    }

    [TestMethod]
    public void FindSkipReason_ShellCommand_SkipsPerlCommand()
    {
        Assert.AreEqual("perl-command", Reason(Server("http") + "<command type=\"shell\">\ntrue\n</command>\n"));
    }

    [TestMethod]
    [DataRow("%PERL -e 1")]
    [DataRow("-K %SRCDIR/test1119.pl")]
    [DataRow("-K %SRCDIR/../docs/options-in-versions")]
    public void FindSkipReason_CommandNamesPerlOrAFileOutsideTheData_SkipsNeedsPerl(string command)
    {
        Assert.AreEqual("needs-perl", Reason(Server("http") + Command(command)));
    }

    [TestMethod]
    public void FindSkipReason_FileNamesAPerlScript_SkipsNeedsPerl()
    {
        Assert.AreEqual("needs-perl", Reason(Server("http") + "<file1 name=\"%LOGDIR/x\">\nrun check.pl\n</file1>\n"));
    }

    [TestMethod]
    [DataRow("-K %SRCDIR/data/data1400.c http://example.pl/")]
    [DataRow("--cacert %SRCDIR/certs/test-ca.crt")]
    public void FindSkipReason_CommandNamesOnlyImportedDataOrAPolishHost_IsRunnable(string command)
    {
        Assert.IsNull(Reason(Server("http") + Command(command)));
    }

    [TestMethod]
    public void FindSkipReason_Precheck_SkipsPrecheck()
    {
        Assert.AreEqual("precheck", Reason(Server("http") + "<precheck>\nsomething\n</precheck>\n"));
    }

    [TestMethod]
    public void FindSkipReason_PostcheckInVerify_SkipsPostcheck()
    {
        Assert.AreEqual("postcheck", Reason(Server("http"), verify: "<postcheck>\nsomething\n</postcheck>\n"));
    }

    [TestMethod]
    public void FindSkipReason_PostcheckInClient_SkipsPostcheck()
    {
        Assert.AreEqual("postcheck", Reason(Server("http") + "<postcheck>\nsomething\n</postcheck>\n"));
    }

    [TestMethod]
    public void FindSkipReason_NoServer_SkipsNoServerHalf()
    {
        Assert.AreEqual("no-server-half", Reason(Command("file://localhost/x")));
    }

    [TestMethod]
    public void FindSkipReason_CaseWithoutAClientSection_SkipsNoServerHalf()
    {
        var testCase = UpstreamTestCaseParser.Parse(UpstreamTestCaseText.Bytes("<testcase>\n<info>\n</info>\n</testcase>\n"), UpstreamTestCaseText.Environment());

        Assert.AreEqual("no-server-half", Selector().FindSkipReason(Number, testCase));
    }

    [TestMethod]
    public void FindSkipReason_CaseWithoutAVerifySection_IsRunnable()
    {
        var testCase = UpstreamTestCaseParser.Parse(
            UpstreamTestCaseText.Bytes("<testcase>\n<client>\n<server>\nhttp\n</server>\n</client>\n</testcase>\n"),
            UpstreamTestCaseText.Environment());

        Assert.IsNull(Selector().FindSkipReason(Number, testCase));
    }

    [TestMethod]
    [DataRow("file")]
    [DataRow("none")]
    public void FindSkipReason_OnlyFileOrNone_SkipsNoServerHalf(string server)
    {
        Assert.AreEqual("no-server-half", Reason(Server(server)));
    }

    [TestMethod]
    public void FindSkipReason_ServerNotBuilt_SkipsServerNotBuiltWithItsName()
    {
        Assert.AreEqual("server-not-built:http/2", Reason("<server>\nhttp\nhttp/2\n</server>\n"));
    }

    [TestMethod]
    public void FindSkipReason_DnsCaseOnAPinWithoutOverrideDns_SkipsFeatureMissingNotServerNotBuilt()
    {
        Assert.AreEqual("feature-missing:override-dns", Reason("<server>\nhttp\ndns\n</server>\n" + Features("override-dns")));
    }

    [TestMethod]
    public void FindSkipReason_DnsLineNoServerHalfImplements_SkipsServerCommandUnsupportedWithItsName()
    {
        Assert.AreEqual("servercmd-unsupported:HTTPS", Reason(Server("dns"), reply: "<dns>\n# a comment\nHTTPS: 1 .\n</dns>\n"));
    }

    [TestMethod]
    public void FindSkipReason_DnsLineTheDnsServerSupports_IsRunnable()
    {
        var rules = Rules() with { SupportsServerCommand = (server, command) => server == "dns" && command == "A" };
        var testCase = Case(client: "<server>\nhttp\ndns\n</server>\n", reply: "<dns>\nA: 127.0.0.1\n</dns>\n");

        Assert.IsNull(new UpstreamTestCaseSelector(rules).FindSkipReason(Number, testCase));
    }

    [TestMethod]
    public void FindSkipReason_ServerTheTableDoesNotHold_SkipsServerNotBuilt()
    {
        Assert.AreEqual("server-not-built:quic", Reason(Server("quic")));
    }

    [TestMethod]
    public void FindSkipReason_ServerLineWithMoreWords_CountsItsFirstWord()
    {
        Assert.IsNull(Reason(Server("http extra words")));
    }

    [TestMethod]
    public void FindSkipReason_SchemeTheBuildLacks_SkipsProtocolMissing()
    {
        Assert.AreEqual("protocol-missing:rtsp", Reason(Server("rtsp-ipv6")));
    }

    [TestMethod]
    public void FindSkipReason_FeatureTheBuildLacks_SkipsFeatureMissing()
    {
        Assert.AreEqual("feature-missing:HTTP3", Reason(Server("http") + Features("SSL", "HTTP3")));
    }

    [TestMethod]
    public void FindSkipReason_NegatedFeatureTheBuildHas_SkipsFeatureMissingWithTheBang()
    {
        Assert.AreEqual("feature-missing:!SSL", Reason(Server("http") + Features("!HTTP3", "!SSL")));
    }

    [TestMethod]
    public void FindSkipReason_UnsupportedServerCommand_SkipsServerCommandUnsupported()
    {
        Assert.AreEqual("servercmd-unsupported:REPLY", Reason(Server("ftp"), reply: "<servercmd>\n# a comment\nREPLY PASV 500 no\n</servercmd>\n"));
    }

    [TestMethod]
    public void FindSkipReason_ServerCommandTheMainServerSupports_IsRunnable()
    {
        var rules = Rules() with { SupportsServerCommand = (server, command) => server == "ftp" && command == "REPLY" };
        var testCase = Case(client: Server("ftp"), reply: "<servercmd>\nREPLY PASV 500 no\n</servercmd>\n");

        Assert.IsNull(new UpstreamTestCaseSelector(rules).FindSkipReason(Number, testCase));
    }

    [TestMethod]
    public void FindSkipReason_UnsupportedStripLine_SkipsStripPartUnsupported()
    {
        var rules = Rules() with { SupportsStripLine = (part, line) => part == "strip" && line == "^Date:" };
        var testCase = Case(
            client: Server("http"),
            verify: "<strip>\n^Date:\n\n# note\n</strip>\n<strippart>\ns/x/y/e\n</strippart>\n");

        Assert.AreEqual("strippart-unsupported", new UpstreamTestCaseSelector(rules).FindSkipReason(Number, testCase));
    }

    [TestMethod]
    public void FindSkipReason_EveryStripLineSupported_IsRunnable()
    {
        var testCase = Case(client: Server("http"), verify: "<strip>\n^Date:\n</strip>\n<stripfile1>\ns/a/b/\n</stripfile1>\n");

        Assert.IsNull(Selector().FindSkipReason(Number, testCase));
    }

    [TestMethod]
    public void FindSkipReason_CaseOnTheExclusionList_SkipsTestServerChoice()
    {
        var rules = Rules() with { TestServerChoices = new HashSet<int> { Number } };

        Assert.AreEqual("test-server-choice", new UpstreamTestCaseSelector(rules).FindSkipReason(Number, Case(client: Server("http"))));
    }

    [TestMethod]
    public void FindSkipReason_SeveralReasonsApply_GivesTheFirstInTheAdrsOrder()
    {
        Assert.AreEqual("libtest", Reason(Server("dns") + "<tool>\nlib1500\n</tool>\n" + Features("Debug")));
    }

    [TestMethod]
    [DataRow("win-x64", "Schannel")]
    [DataRow("linux-x64", "OpenSSL")]
    [DataRow("osx-arm64", "OpenSSL")]
    public void FindSkipReason_HttpsCaseNeedingTheReferenceBuildsTlsBackend_IsRunnableOnItsPlatform(string platform, string backend)
    {
        var pin = ReferencePins().Single(candidate => candidate.Platform == platform);
        var rules = UpstreamTestCaseSelectionRules.ForBuild(pin, new HashSet<int>(), new HashSet<int>());
        var testCase = Case(client: Server("https") + Features("SSL", backend, "proxy", "!Debug"));

        Assert.IsNull(new UpstreamTestCaseSelector(rules).FindSkipReason(Number, testCase));
    }

    [TestMethod]
    [DataRow(320)]
    [DataRow(321)]
    [DataRow(322)]
    [DataRow(324)]
    public void FindSkipReason_ImportedTlsSrpCaseForTheWindowsReferencePin_SkipsFeatureMissingTlsSrp(int number)
    {
        var (selector, testCase) = ImportedCaseFor("win-x64", number);

        Assert.AreEqual("feature-missing:TLS-SRP", selector.FindSkipReason(number, testCase));
    }

    [TestMethod]
    [DataRow("linux-x64", 320)]
    [DataRow("linux-x64", 324)]
    [DataRow("osx-arm64", 321)]
    [DataRow("osx-arm64", 322)]
    public void FindSkipReason_ImportedTlsSrpCaseForAReferencePinWithTlsSrp_IsRunnable(string platform, int number)
    {
        var (selector, testCase) = ImportedCaseFor(platform, number);

        Assert.IsNull(selector.FindSkipReason(number, testCase));
    }

    [TestMethod]
    public void ForClient_PortVersionLackingSmb_SkipsAnSmbCaseAndRunsAnHttpCase()
    {
        var report = CurlVersionReport.Parse(CurlVersionReportTests.PortVersionOutput);
        var selector = new UpstreamTestCaseSelector(UpstreamTestCaseSelectionRules.ForClient(report, new HashSet<int>(), new HashSet<int>()));

        Assert.AreEqual("protocol-missing:smb", selector.FindSkipReason(Number, Case(client: Server("smb"))));
        Assert.IsNull(selector.FindSkipReason(Number, Case(client: Server("http") + Features("SSL", "Schannel", "brotli"))));
    }

    [TestMethod]
    public void ForClient_NullArguments_Throw()
    {
        var report = CurlVersionReport.Parse(CurlVersionReportTests.PortVersionOutput);

        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseSelectionRules.ForClient(null!, new HashSet<int>(), new HashSet<int>()));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseSelectionRules.ForClient(report, null!, new HashSet<int>()));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseSelectionRules.ForClient(report, new HashSet<int>(), null!));
    }

    [TestMethod]
    public void ForBuild_NullArguments_Throw()
    {
        var pin = ReferencePins()[0];

        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseSelectionRules.ForBuild(null!, new HashSet<int>(), new HashSet<int>()));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseSelectionRules.ForBuild(pin, null!, new HashSet<int>()));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseSelectionRules.ForBuild(pin, new HashSet<int>(), null!));
    }

    [TestMethod]
    public void ForBuild_AsSurlStandsToday_AcceptsTheBuiltServerHalvesCommandsAndTheInterpretedStripLinesAndExcludesTheGivenCases()
    {
        var rules = UpstreamTestCaseSelectionRules.ForBuild(ReferencePins()[0], new HashSet<int>(), new HashSet<int> { 802 });

        Assert.IsTrue(rules.SupportsServerCommand("ftp", "REPLY"));
        Assert.IsFalse(rules.SupportsServerCommand("ftp", "DELAY"));
        Assert.IsTrue(rules.SupportsServerCommand("smtp", "REPLY"));
        Assert.IsFalse(rules.SupportsServerCommand("smtp", "SLOWDOWN"));
        Assert.IsFalse(rules.SupportsServerCommand("imaps", "REPLY"));
        Assert.IsTrue(rules.SupportsServerCommand("http", "writedelay:"));
        Assert.IsFalse(rules.SupportsServerCommand("http", "upgrade"));
        Assert.IsTrue(rules.SupportsStripLine("strip", "^Date:"));
        Assert.IsTrue(rules.SupportsStripLine("strippart", "s/^Date:.*//"));
        Assert.IsFalse(rules.SupportsStripLine("strippart", "chomp;"));
        CollectionAssert.AreEquivalent(new[] { 802 }, rules.TestServerChoices.ToList());
        Assert.Contains("http", rules.Protocols);
    }

    [TestMethod]
    [DataRow("http", "<servercmd>\nwritedelay: 10\nskip: 4\n</servercmd>\n", null)]
    [DataRow("https", "<servercmd>\nauth_required\nswsclose\n</servercmd>\n", null)]
    [DataRow("http", "<servercmd>\nupgrade\n</servercmd>\n", "servercmd-unsupported:upgrade")]
    [DataRow("http", "<servercmd>\nconnection-monitor\n</servercmd>\n", "servercmd-unsupported:connection-monitor")]
    [DataRow("http", "<postcmd>\nwait 1\n</postcmd>\n", "servercmd-unsupported:postcmd")]
    [DataRow("http", "<postcmd>\n# nothing\n</postcmd>\n", null)]
    [DataRow("ftp", "<servercmd>\nidle\n</servercmd>\n", "servercmd-unsupported:idle")]
    public void FindSkipReason_ServerCommandsUnderTodaysRules_RunOrSkipByTheServerHalfsTable(string server, string reply, string? expected)
    {
        var rules = Rules() with { SupportsServerCommand = UpstreamTestCaseServerCommands.IsSupported };

        var reason = new UpstreamTestCaseSelector(rules).FindSkipReason(Number, Case(client: Server(server), reply: reply));

        Assert.AreEqual(expected, reason);
    }

    // BL-898: a command is supported when any named server's row implements it.
    [TestMethod]
    [DataRow("http\nimap", "AUTH", null)]
    [DataRow("http\nimap", "upgrade", "servercmd-unsupported:upgrade")]
    [DataRow("imap\nhttp", "swsclose", null)]
    [DataRow("http\nimap", "DELAY", "servercmd-unsupported:DELAY")]
    public void FindSkipReason_SeveralServers_SupportsACommandAnyNamedServerImplements(string servers, string word, string? expected)
    {
        var rules = Rules() with
        {
            SupportsServerCommand = UpstreamTestCaseServerCommands.IsSupported,
            Protocols = new HashSet<string>(["http", "imap"], StringComparer.Ordinal),
        };
        var testCase = Case(client: Server(servers), reply: $"<servercmd>\n{word}\n</servercmd>\n");

        var reason = new UpstreamTestCaseSelector(rules).FindSkipReason(Number, testCase);

        Assert.AreEqual(expected, reason);
    }

    // BL-397: the file-system servers' cases, inline, on Windows' reference build (no smb) and on
    // the supplementary build ADR-0030 pinned for SMB, each excluded class with its named reason.
    [TestMethod]
    [DataRow("0E773709", "sftp", "", null)]
    [DataRow("0E773709", "scp", "", null)]
    [DataRow("0E773709", "sftp", "<features>\nsftp\n!badlibssh\n</features>\n", null)]
    [DataRow("0E773709", "smb", "<features>\nsmb\n</features>\n", "protocol-missing:smb")]
    [DataRow("589C8E4D", "smb", "<features>\nsmb\n</features>\n", null)]
    [DataRow("0E773709", "sftp", "<precheck>\n%PERL %SRCDIR/libtest/test613.pl prepare %PWD/%LOGDIR/test.dir\n</precheck>\n", "precheck")]
    [DataRow("0E773709", "sftp", "<tool>\nlib%TESTNUMBER\n</tool>\n", "libtest")]
    [DataRow("0E773709", "sftp", "<tool>\nunit2604\n</tool>\n", "unittest")]
    [DataRow("0E773709", "sftp", "", "servercmd-unsupported:Testnum", "<servercmd>\nTestnum 1\n</servercmd>\n")]
    public void FindSkipReason_FileSystemServersCases_RunOrSkipWithTheirNamedReason(
        string pinPrefix, string server, string client, string? expected, string reply = "")
    {
        var pin = AllPins().Single(candidate => candidate.Sha256.StartsWith(pinPrefix, StringComparison.Ordinal));
        var rules = UpstreamTestCaseSelectionRules.ForBuild(pin, new HashSet<int>(), new HashSet<int>());
        var testCase = Case(client: Server(server) + client + Command($"-u %USER: {server}://%HOSTIP:%SSHPORT/x"), reply: reply);

        var reason = new UpstreamTestCaseSelector(rules).FindSkipReason(Number, testCase);

        Assert.AreEqual(expected, reason);
    }

    [TestMethod]
    public void FindSkipReason_FileSystemServersCaseWithAPostcheck_SkipsPostcheck()
    {
        var rules = UpstreamTestCaseSelectionRules.ForBuild(AllPins().Single(pin => pin.Sha256.StartsWith("0E773709", StringComparison.Ordinal)), new HashSet<int>(), new HashSet<int>());
        var testCase = Case(client: Server("sftp"), verify: "<postcheck>\n%PERL %SRCDIR/libtest/test610.pl gone %PWD/%LOGDIR/test.dir\n</postcheck>\n");

        Assert.AreEqual("postcheck", new UpstreamTestCaseSelector(rules).FindSkipReason(Number, testCase));
    }

    private static IReadOnlyList<PinnedUpstreamCurlBuild> AllPins() =>
        UpstreamCurlBuildPins.Parse(File.ReadAllText(Path.Combine(PinnedUpstreamCurl.RepositoryRoot(), UpstreamCurlBuildPins.FileName)));

    internal static List<PinnedUpstreamCurlBuild> ReferencePins() =>
        UpstreamCurlBuildPins.Parse(File.ReadAllText(Path.Combine(PinnedUpstreamCurl.RepositoryRoot(), UpstreamCurlBuildPins.FileName)))
            .Where(pin => pin.Role == UpstreamCurlBuildRole.Reference && pin.Kind == UpstreamCurlBuildKind.Curl)
            .ToList();

    // An imported case parsed for selection as the run parses it: the pin's features, no SRCDIR.
    private static (UpstreamTestCaseSelector Selector, UpstreamTestCase TestCase) ImportedCaseFor(string platform, int number)
    {
        var pin = ReferencePins().Single(candidate => candidate.Platform == platform);
        var features = UpstreamCurlBuildFeatures.For(pin);
        var environment = new UpstreamTestCaseEnvironment(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["CERTDIR"] = "." }, features, new UnitTestNoFileReader(), TimeProvider.System);
        var path = Path.Combine(AppContext.BaseDirectory, "UpstreamTestCases", "data", $"test{number}");

        return (
            new UpstreamTestCaseSelector(UpstreamTestCaseSelectionRules.ForBuild(pin, new HashSet<int>(), new HashSet<int>())),
            UpstreamTestCaseParser.Parse(File.ReadAllBytes(path), environment));
    }

    private static string? Reason(string client, string reply = "", string verify = "") =>
        Selector().FindSkipReason(Number, Case(client, reply, verify));

    private static UpstreamTestCaseSelector Selector() => new(Rules());

    private static UpstreamTestCaseSelectionRules Rules() => new(
        new HashSet<int>(),
        new HashSet<string>(["http", "https", "ftp"], StringComparer.Ordinal),
        new HashSet<string>(["SSL", "IPv6"], StringComparer.Ordinal),
        static (_, _) => false,
        static (_, _) => true,
        new HashSet<int>());

    private static string Server(string name) => $"<server>\n{name}\n</server>\n";

    private static string Command(string command) => $"<command>\n{command}\n</command>\n";

    private static string Features(params string[] features) => $"<features>\n{string.Join("\n", features)}\n</features>\n";

    private static UpstreamTestCase Case(string client, string reply = "", string verify = "") =>
        UpstreamTestCaseParser.Parse(
            UpstreamTestCaseText.Bytes($"<testcase>\n<reply>\n{reply}</reply>\n<client>\n{client}</client>\n<verify>\n{verify}</verify>\n</testcase>\n"),
            UpstreamTestCaseText.Environment());

    private sealed class UnitTestNoFileReader : IUpstreamTestCaseFileReader
    {
        public byte[] ReadAllBytes(string path) => [];
    }
}
