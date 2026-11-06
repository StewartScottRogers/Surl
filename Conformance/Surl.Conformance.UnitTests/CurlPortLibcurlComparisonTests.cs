namespace Surl.Conformance;

[TestClass]
public sealed class CurlPortLibcurlComparisonTests
{
    private const string SurlCommit = "6b47feae";
    private const string Performed = "perform: CURLcode 0 (No error)";
    private const string Echoed = "recv: CURLcode 0 (No error), flags TEXT, offset 0, bytesleft 0, 5 bytes \"hello\"";

    private static readonly PinnedUpstreamCurlBuild UpstreamLibrary = new(
        "win-x64", "C:/libcurl-4.dll", new string('7', 64), "libcurl/8.21.0", [], UpstreamCurlBuildRole.Reference, UpstreamCurlBuildKind.Library);

    private static readonly PinnedCurlPortClient PortLibrary = CurlPortClientLocatorTests.Pin("/libcurl/Libcurl.dll", [0x20]);

    [TestMethod]
    public void Compare_SameReportsNotesAndFiles_IsAgreement()
    {
        var verdict = Compare(Case(), Run([Performed, Echoed], port: 1000), Run([Performed, Echoed], port: 2000));

        Assert.AreEqual(CurlPortVerdictKind.Agreement, verdict.Kind);
    }

    [TestMethod]
    public void Compare_UpstreamFailsTheMirroredAssertions_IsASurlDefectAndThePortIsNotJudged()
    {
        var verdict = Compare(Case(_ => "lines[1] differs"), Run([Performed]), Run(["anything"]));

        Assert.AreEqual(CurlPortVerdictKind.SurlDefect, verdict.Kind);
        Assert.AreEqual("lines[1] differs", verdict.Reason);
    }

    [TestMethod]
    public void Compare_UpstreamDriverDidNotExit0_IsASurlDefect()
    {
        var verdict = Compare(Case(), Run([], exitCode: 1, standardError: "boom"), Run([]));

        Assert.AreEqual(CurlPortVerdictKind.SurlDefect, verdict.Kind);
        StringAssert.Contains(verdict.Reason, "exited 1: boom");
    }

    [TestMethod]
    public void Compare_PortReportDiffers_IsAPortDefectNamingTheFirstDifferingLine()
    {
        var verdict = Compare(Case(), Run([Performed, Echoed]), Run([Performed, "recv: CURLcode 52 (x)"]));

        Assert.AreEqual(CurlPortVerdictKind.PortDefect, verdict.Kind);
        var finding = verdict.Finding!;
        Assert.AreEqual("Text_Echo_ComesBackWhole", finding.Test);
        Assert.AreEqual("Run-LibcurlWebSocketScript.cs ws://{surl}/echo send:TEXT:hello recv", finding.CommandLine);
        Assert.AreSame(UpstreamLibrary, finding.UpstreamBuild);
        Assert.AreSame(PortLibrary, finding.PortClient);
        Assert.AreEqual(SurlCommit, finding.SurlCommit);
        var difference = finding.Differences.Single();
        Assert.AreEqual("driver report", difference.Item);
        Assert.AreEqual($"line 2: {Echoed}", difference.Upstream);
        Assert.AreEqual(0, finding.Port.ExitCode);
        Assert.AreEqual($"{Performed}\nrecv: CURLcode 52 (x)", System.Text.Encoding.UTF8.GetString(finding.Port.StandardOutput));
    }

    [TestMethod]
    public void Compare_PortDriverExitsOtherwise_IsAPortDefectOnTheExitCode()
    {
        var verdict = Compare(Case(), Run([Performed]), Run([Performed], exitCode: 1));

        Assert.AreEqual("driver exit code: upstream 0; port 1", verdict.Finding!.Differences.Single().ToString());
    }

    [TestMethod]
    public void Compare_NotesDifferBeyondPortsAndSessions_IsAPortDefect()
    {
        var upstream = Run([Performed], notes: "#1 * Exchange 1 opened: rtsp from 127.0.0.1:60001.\n#1 session 4A98BFB49ED4CCE2\n");
        var port = Run([Performed], notes: "#1 * Exchange 1 opened: rtsp from 127.0.0.1:60002.\n#1 session 5EAA44EBD79618CE\n#2 * Exchange 2 opened: rtsp from 127.0.0.1:60003.\n");

        var verdict = Compare(Case(), upstream, port);

        var difference = verdict.Finding!.Differences.Single();
        Assert.AreEqual("surl notes", difference.Item);
        Assert.AreEqual("line 3: ", difference.Upstream);
    }

    [TestMethod]
    public void Compare_StoredFileDiffers_IsAPortDefectOnThatFile()
    {
        var libcurlCase = Case() with { ComparedStoredFiles = ["rec.bin.sdp", "rec.bin"] };
        var upstream = Run([Performed], stored: new() { ["rec.bin.sdp"] = "v=0\r\n"u8.ToArray() });
        var port = Run([Performed], stored: []);

        var verdict = Compare(libcurlCase, upstream, port);

        Assert.AreEqual("stored file rec.bin.sdp: upstream 5 bytes; port (absent)", verdict.Finding!.Differences.Single().ToString());
    }

    [TestMethod]
    public void Compare_NullArguments_Throw()
    {
        var run = Run([]);

        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortLibcurlComparison.Compare(null!, run, UpstreamLibrary, run, PortLibrary, SurlCommit));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortLibcurlComparison.Compare(Case(), null!, UpstreamLibrary, run, PortLibrary, SurlCommit));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortLibcurlComparison.Compare(Case(), run, null!, run, PortLibrary, SurlCommit));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortLibcurlComparison.Compare(Case(), run, UpstreamLibrary, null!, PortLibrary, SurlCommit));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortLibcurlComparison.Compare(Case(), run, UpstreamLibrary, run, null!, SurlCommit));
        Assert.ThrowsExactly<ArgumentException>(() => CurlPortLibcurlComparison.Compare(Case(), run, UpstreamLibrary, run, PortLibrary, " "));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortLibcurlComparison.NormaliseNotes(null!));
    }

    [TestMethod]
    public void NormaliseNotes_PortAndSession_AreReplaced()
    {
        var surl = new CurlPortSurlSide(554, "#1 rtsp://127.0.0.1:554/ session 4A98BFB49ED4CCE2", new Dictionary<string, byte[]>());

        Assert.AreEqual("#1 rtsp://127.0.0.1:<port>/ session <session>", CurlPortLibcurlComparison.NormaliseNotes(surl));
    }

    private static CurlPortLibcurlCase Case(Func<CurlPortLibcurlRun, string?>? check = null) => new(
        "Text_Echo_ComesBackWhole",
        "PinnedLibcurlTalksToSurlOverWebSocketTests.Text_Echo_ComesBackWhole",
        "Run-LibcurlWebSocketScript.cs",
        ["ws://{surl}/echo", "send:TEXT:hello", "recv"],
        check ?? (_ => null));

    private static CurlPortLibcurlRun Run(
        string[] lines, int exitCode = 0, string standardError = "", int port = 1000, string notes = "", Dictionary<string, byte[]>? stored = null) =>
        new(exitCode, lines, standardError, new CurlPortSurlSide(port, notes, stored ?? []));

    private static CurlPortVerdict Compare(CurlPortLibcurlCase libcurlCase, CurlPortLibcurlRun upstream, CurlPortLibcurlRun port) =>
        CurlPortLibcurlComparison.Compare(libcurlCase, upstream, UpstreamLibrary, port, PortLibrary, SurlCommit);
}
