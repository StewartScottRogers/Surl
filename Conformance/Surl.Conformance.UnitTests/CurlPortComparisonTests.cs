using System.Text;

namespace Surl.Conformance;

[TestClass]
public sealed class CurlPortComparisonTests
{
    private const int UpstreamSurlPort = 50101;
    private const int PortSurlPort = 50202;
    private const string SurlCommit = "0123456789abcdef0123456789abcdef01234567";

    private static readonly PinnedUpstreamCurlBuild ReferenceBuild = new(
        "win-x64", "/upstream/8.21.0/curl", "AA11", "curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0", ["http"], UpstreamCurlBuildRole.Reference);

    private static readonly PinnedUpstreamCurlBuild LaterBuild = new(
        "win-x64", "/upstream/8.22.0/curl", "BB22", "curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0", ["smb"], UpstreamCurlBuildRole.Supplementary);

    private static readonly PinnedCurlPortClient PortClient = CurlPortClientLocatorTests.Pin("/curl-port/abc123/curl", [0x4D, 0x5A, 0x10]);

    private static readonly CurlPortSurlSide UpstreamSurl = new(UpstreamSurlPort, string.Empty, new Dictionary<string, byte[]>());
    private static readonly CurlPortSurlSide PortSurl = new(PortSurlPort, string.Empty, new Dictionary<string, byte[]>());

    [TestMethod]
    public void Compare_SameRuns_IsAnAgreement()
    {
        var verdict = Compare(Case(), Upstream(0, "hello\n"), UpstreamSurl, Port(0, "hello\n"), PortSurl);

        Assert.AreEqual(CurlPortVerdictKind.Agreement, verdict.Kind);
        Assert.IsNull(verdict.Finding);
        Assert.AreEqual(string.Empty, verdict.Reason);
    }

    [TestMethod]
    public void Compare_PortExitCodeDiffers_IsAPortDefect()
    {
        var verdict = Compare(Case(), Upstream(0, "hello\n"), UpstreamSurl, Port(7, "hello\n"), PortSurl);

        Assert.AreEqual(CurlPortVerdictKind.PortDefect, verdict.Kind);
        Assert.AreEqual(new CurlPortDifference("exit code", "0", "7"), verdict.Finding!.Differences.Single());
    }

    [TestMethod]
    public void Compare_UpstreamIsNotWhatTheMirroredTestAsserts_IsASurlDefectAndThePortIsNotJudged()
    {
        var verdict = Compare(Case(), Upstream(56, string.Empty), UpstreamSurl, Port(7, "other"), PortSurl);

        Assert.AreEqual(CurlPortVerdictKind.SurlDefect, verdict.Kind);
        Assert.AreEqual("upstream exited 56, not 0", verdict.Reason);
        Assert.IsNull(verdict.Finding);
    }

    [TestMethod]
    public void Compare_DifferenceTheReleaseNotesDescribe_OnALaterBuild_IsNotComparable()
    {
        var measurementCase = Case() with { ReleaseDifference = "curl 8.22.0 RELEASE-NOTES: smb: example change" };

        var verdict = Compare(measurementCase, Upstream(0, "hello\n", LaterBuild), UpstreamSurl, Port(0, "hello!\n"), PortSurl);

        Assert.AreEqual(CurlPortVerdictKind.NotComparable, verdict.Kind);
        Assert.AreEqual(
            "release difference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0: curl 8.22.0 RELEASE-NOTES: smb: example change",
            verdict.Reason);
    }

    [TestMethod]
    public void Compare_ReleaseDifferenceNamed_OnTheReferenceRelease_IsStillAPortDefect()
    {
        var measurementCase = Case() with { ReleaseDifference = "an entry" };

        var verdict = Compare(measurementCase, Upstream(0, "hello\n"), UpstreamSurl, Port(0, "hello!\n"), PortSurl);

        Assert.AreEqual(CurlPortVerdictKind.PortDefect, verdict.Kind);
    }

    [TestMethod]
    public void Compare_DifferenceOnALaterBuild_WithNoReleaseDifferenceNamed_IsAPortDefect()
    {
        var verdict = Compare(Case(), Upstream(0, "hello\n", LaterBuild), UpstreamSurl, Port(0, "hello!\n"), PortSurl);

        Assert.AreEqual(CurlPortVerdictKind.PortDefect, verdict.Kind);
    }

    [TestMethod]
    public void Compare_Finding_CarriesEveryFieldAdr0089Decision6Lists()
    {
        var upstream = Upstream(0, "hello\n");
        var port = Port(0, "hullo\n");

        var finding = Compare(Case(), upstream, UpstreamSurl, port, PortSurl).Finding!;

        Assert.AreEqual("Get_HelloTxt", finding.Test);
        Assert.AreEqual("UpstreamCurlFetchesFromSurlOverHttp11Tests.Get_ServesTheFile", finding.Mirrors);
        Assert.AreEqual("curl -s -S {surl}/hello.txt", finding.CommandLine);
        Assert.AreEqual("AA11", finding.UpstreamBuild.Sha256);
        Assert.AreEqual(ReferenceBuild.Version, finding.UpstreamBuild.Version);
        Assert.AreEqual("abc123", finding.PortClient.Commit);
        Assert.AreEqual(PortClient.Sha256, finding.PortClient.Sha256);
        Assert.AreEqual(SurlCommit, finding.SurlCommit);
        CollectionAssert.AreEqual(upstream.StandardOutput, finding.Upstream.StandardOutput);
        CollectionAssert.AreEqual(port.StandardOutput, finding.Port.StandardOutput);
        Assert.AreEqual(0, finding.Upstream.ExitCode);
        Assert.AreEqual(0, finding.Port.ExitCode);
        Assert.AreEqual(new CurlPortDifference("standard output", "line 1: hello", "line 1: hullo"), finding.Differences.Single());

        var text = finding.ToString();
        StringAssert.Contains(text, "Phase 7 finding in Get_HelloTxt, mirroring UpstreamCurlFetchesFromSurlOverHttp11Tests.Get_ServesTheFile");
        StringAssert.Contains(text, "command line: curl -s -S {surl}/hello.txt");
        StringAssert.Contains(text, $"upstream curl: AA11 ({ReferenceBuild.Version})");
        StringAssert.Contains(text, $"Curl port: commit abc123 ({PortClient.Sha256})");
        StringAssert.Contains(text, $"Surl: commit {SurlCommit}");
        StringAssert.Contains(text, "standard output: upstream line 1: hello; port line 1: hullo");
    }

    [TestMethod]
    public void Compare_SurlPortInStandardOutput_AgreesButADifferenceElsewhereIsAFinding()
    {
        AssertOnlyNormalisedFieldDiffers(
            $"http://127.0.0.1:{UpstreamSurlPort}/hello.txt\n",
            $"http://127.0.0.1:{PortSurlPort}/hello.txt\n",
            $"http://127.0.0.1:{PortSurlPort}/hullo.txt\n");
    }

    [TestMethod]
    public void Compare_DateAndLastModifiedValues_AgreeButADifferenceElsewhereIsAFinding()
    {
        AssertOnlyNormalisedFieldDiffers(
            "HTTP/1.1 200 OK\r\nDate: Wed, 04 Nov 2026 10:00:00 GMT\r\nLast-Modified: Tue, 03 Nov 2026 09:00:00 GMT\r\n\r\n",
            "HTTP/1.1 200 OK\r\nDate: Wed, 04 Nov 2026 10:00:01 GMT\r\nLast-Modified: Tue, 03 Nov 2026 09:00:05 GMT\r\n\r\n",
            "HTTP/1.1 404 OK\r\nDate: Wed, 04 Nov 2026 10:00:01 GMT\r\nLast-Modified: Tue, 03 Nov 2026 09:00:05 GMT\r\n\r\n");
    }

    [TestMethod]
    public void Compare_WriteOutTimeAndSpeedValues_AgreeButADifferenceElsewhereIsAFinding()
    {
        var measurementCase = Case() with { Arguments = ["-s", "-w", "%{http_code} %{time_total}s %{speed_download}B/s\\n", "{surl}/hello.txt"] };

        Assert.AreEqual(
            CurlPortVerdictKind.Agreement,
            Compare(measurementCase, Upstream(0, "hello\n200 0.001234s 4321B/s\n"), UpstreamSurl, Port(0, "hello\n200 0.020000s 99B/s\n"), PortSurl).Kind);
        Assert.AreEqual(
            CurlPortVerdictKind.PortDefect,
            Compare(measurementCase, Upstream(0, "hello\n200 0.001234s 4321B/s\n"), UpstreamSurl, Port(0, "hello\n404 0.020000s 99B/s\n"), PortSurl).Kind);
    }

    [TestMethod]
    public void Compare_StandardErrorLinesTheMirroredTestDoesNotAssertOn_AgreeButAnAssertedLineIsAFinding()
    {
        var measurementCase = Case() with { AssertedStandardErrorFragments = ["curl: (22)"] };

        Assert.AreEqual(
            CurlPortVerdictKind.Agreement,
            Compare(
                measurementCase,
                Upstream(0, "hello\n", standardError: "* upstream wording\r\ncurl: (22) The requested URL returned error: 404\r\n"),
                UpstreamSurl,
                Port(0, "hello\n", standardError: "port wording\ncurl: (22) The requested URL returned error: 404\n"),
                PortSurl).Kind);
        var finding = Compare(
            measurementCase,
            Upstream(0, "hello\n", standardError: "curl: (22) The requested URL returned error: 404\n"),
            UpstreamSurl,
            Port(0, "hello\n", standardError: "curl: (22) error 404\n"),
            PortSurl).Finding!;
        Assert.AreEqual(
            new CurlPortDifference("standard error", "line 1: curl: (22) The requested URL returned error: 404", "line 1: curl: (22) error 404"),
            finding.Differences.Single());
    }

    [TestMethod]
    public void Compare_NoAssertedStandardError_ComparesNone()
    {
        Assert.AreEqual(
            CurlPortVerdictKind.Agreement,
            Compare(Case(), Upstream(0, "hello\n", standardError: "a"), UpstreamSurl, Port(0, "hello\n", standardError: "b"), PortSurl).Kind);
    }

    [TestMethod]
    public void Compare_PortsAndTimeStampsInSurlNotes_AgreeButADifferenceElsewhereIsAFinding()
    {
        var upstreamSurl = UpstreamSurl with { Notes = $"10:00:00.123456 Listening on 127.0.0.1:{UpstreamSurlPort}\n10:00:00.200000 Connection from 127.0.0.1:61001\n> GET /hello.txt\n" };
        var agreeingSurl = PortSurl with { Notes = $"11:30:59.999999 Listening on 127.0.0.1:{PortSurlPort}\n11:31:00.000001 Connection from 127.0.0.1:62002\n> GET /hello.txt\n" };
        var differingSurl = agreeingSurl with { Notes = agreeingSurl.Notes.Replace("/hello.txt", "/hello.txt?", StringComparison.Ordinal) };

        Assert.AreEqual(
            CurlPortVerdictKind.Agreement,
            Compare(Case(), Upstream(0, "hello\n"), upstreamSurl, Port(0, "hello\n"), agreeingSurl).Kind);
        Assert.AreEqual(
            new CurlPortDifference("surl notes", "line 3: > GET /hello.txt", "line 3: > GET /hello.txt?"),
            Compare(Case(), Upstream(0, "hello\n"), upstreamSurl, Port(0, "hello\n"), differingSurl).Finding!.Differences.Single());
    }

    [TestMethod]
    public void Compare_SurlPortInRequestBytes_AgreesButADifferenceElsewhereIsAFinding()
    {
        var upstreamSurl = UpstreamSurl with { ClientRequestBytes = [Encoding.Latin1.GetBytes($"GET / HTTP/1.1\r\nHost: 127.0.0.1:{UpstreamSurlPort}\r\n\r\n")] };
        var agreeingSurl = PortSurl with { ClientRequestBytes = [Encoding.Latin1.GetBytes($"GET / HTTP/1.1\r\nHost: 127.0.0.1:{PortSurlPort}\r\n\r\n")] };
        var differingSurl = PortSurl with { ClientRequestBytes = [Encoding.Latin1.GetBytes($"GET / HTTP/1.0\r\nHost: 127.0.0.1:{PortSurlPort}\r\n\r\n")] };

        Assert.AreEqual(
            CurlPortVerdictKind.Agreement,
            Compare(Case(), Upstream(0, "hello\n"), upstreamSurl, Port(0, "hello\n"), agreeingSurl).Kind);
        Assert.AreEqual(
            new CurlPortDifference("request bytes on connection 0", "byte 0x31 at offset 13", "byte 0x30 at offset 13"),
            Compare(Case(), Upstream(0, "hello\n"), upstreamSurl, Port(0, "hello\n"), differingSurl).Finding!.Differences.Single());
    }

    [TestMethod]
    public void Compare_RelayedTranscriptsDifferingOnlyInNormalisedFields_AgreeButAnyOtherByteIsAFindingNamingConnectionAndOffset()
    {
        const int UpstreamRelayPort = 61001;
        const int PortRelayPort = 62002;
        var upstreamSurl = UpstreamSurl with
        {
            RelayPort = UpstreamRelayPort,
            ClientRequestBytes = Transcript(
                $"GET /a HTTP/1.1\r\nHost: 127.0.0.1:{UpstreamRelayPort}\r\nUser-Agent: curl/8.21.0\r\n\r\n",
                $"GET /b HTTP/1.1\r\nHost: 127.0.0.1:{UpstreamRelayPort}\r\nUser-Agent: curl/8.21.0\r\n\r\n"),
        };
        var agreeingSurl = PortSurl with
        {
            RelayPort = PortRelayPort,
            ClientRequestBytes = Transcript(
                $"GET /a HTTP/1.1\r\nHost: 127.0.0.1:{PortRelayPort}\r\nUser-Agent: curl/8.21.0-port\r\n\r\n",
                $"GET /b HTTP/1.1\r\nHost: 127.0.0.1:{PortRelayPort}\r\nUser-Agent: other\r\n\r\n"),
        };
        var differingSurl = agreeingSurl with
        {
            ClientRequestBytes = Transcript(
                $"GET /a HTTP/1.1\r\nHost: 127.0.0.1:{PortRelayPort}\r\nUser-Agent: x\r\n\r\n",
                $"GET /b HTTP/1.1\r\nHost: 127.0.0.1:{PortRelayPort}\r\nAccept: */*\r\n\r\n"),
        };

        Assert.AreEqual(
            CurlPortVerdictKind.Agreement,
            Compare(Case(), Upstream(0, "hello\n"), upstreamSurl, Port(0, "hello\n"), agreeingSurl).Kind);
        Assert.AreEqual(
            new CurlPortDifference("request bytes on connection 1", "byte 0x55 at offset 41", "byte 0x41 at offset 41"),
            Compare(Case(), Upstream(0, "hello\n"), upstreamSurl, Port(0, "hello\n"), differingSurl).Finding!.Differences.Single());
    }

    [TestMethod]
    public void Compare_RelayedRun_NormalisesTheRelayPortTheClientNamesInItsOutput()
    {
        var upstreamSurl = UpstreamSurl with { RelayPort = 61001 };
        var portSurl = PortSurl with { RelayPort = 62002 };

        Assert.AreEqual(
            CurlPortVerdictKind.Agreement,
            Compare(Case(), Upstream(0, "http://127.0.0.1:61001/\n"), upstreamSurl, Port(0, "http://127.0.0.1:62002/\n"), portSurl).Kind);
        Assert.AreEqual(61001, upstreamSurl.ClientFacingPort);
        Assert.AreEqual(UpstreamSurlPort, UpstreamSurl.ClientFacingPort);
    }

    [TestMethod]
    public void Compare_RequestBytes_AnExtraConnectionOrAShorterOneIsAFinding()
    {
        var upstreamSurl = UpstreamSurl with { ClientRequestBytes = [[0x41, 0x42]] };
        var portSurl = PortSurl with { ClientRequestBytes = [[0x41], [0x43]] };

        var differences = Compare(Case(), Upstream(0, "hello\n"), upstreamSurl, Port(0, "hello\n"), portSurl).Finding!.Differences;

        CollectionAssert.AreEqual(
            new[]
            {
                new CurlPortDifference("request bytes on connection 0", "byte 0x42 at offset 1", "end at offset 1"),
                new CurlPortDifference("request bytes on connection 1", "(absent)", "1 bytes"),
            },
            differences.ToArray());
    }

    [TestMethod]
    public void Compare_RequestBytesRecordedForOneRunOnly_AreNotCompared()
    {
        var upstreamSurl = UpstreamSurl with { ClientRequestBytes = [[0x41]] };

        Assert.AreEqual(
            CurlPortVerdictKind.Agreement,
            Compare(Case(), Upstream(0, "hello\n"), upstreamSurl, Port(0, "hello\n"), PortSurl).Kind);
        Assert.AreEqual(
            CurlPortVerdictKind.Agreement,
            Compare(Case(), Upstream(0, "hello\n"), UpstreamSurl, Port(0, "hello\n"), upstreamSurl).Kind);
    }

    [TestMethod]
    public void Compare_StoredFiles_AreComparedByteForByteWhereTheCaseNamesThem()
    {
        var measurementCase = Case() with { ComparedStoredFiles = ["up.txt", "gone.txt", "neither.txt"] };
        var upstreamSurl = UpstreamSurl with { StoredFiles = new Dictionary<string, byte[]> { ["up.txt"] = [0x61], ["gone.txt"] = [0x62], ["unnamed.txt"] = [0x01] } };
        var portSurl = PortSurl with { StoredFiles = new Dictionary<string, byte[]> { ["up.txt"] = [0x61, 0x0A], ["unnamed.txt"] = [0x02] } };

        var differences = Compare(measurementCase, Upstream(0, "hello\n"), upstreamSurl, Port(0, "hello\n"), portSurl).Finding!.Differences;

        CollectionAssert.AreEqual(
            new[]
            {
                new CurlPortDifference("stored file up.txt", "end at offset 1", "byte 0x0A at offset 1"),
                new CurlPortDifference("stored file gone.txt", "1 bytes", "(absent)"),
            },
            differences.ToArray());
    }

    [TestMethod]
    public void Compare_PortStoppedAtItsTimeout_NamesTheTimeout()
    {
        var port = CurlPortClientRunResult.StoppedAtTimeout(PortClient, Encoding.Latin1.GetBytes("hello\n"), string.Empty);

        var verdict = Compare(Case(), Upstream(0, "hello\n"), UpstreamSurl, port, PortSurl);

        Assert.AreEqual(new CurlPortDifference("exit code", "0", "stopped at its timeout"), verdict.Finding!.Differences.Single());
    }

    [TestMethod]
    public void Compare_PortWritesFewerLines_NamesTheMissingLine()
    {
        var verdict = Compare(Case(), Upstream(0, "hello\nworld"), UpstreamSurl, Port(0, "hello"), PortSurl);

        Assert.AreEqual(new CurlPortDifference("standard output", "line 2: world", "line 2: (no such line)"), verdict.Finding!.Differences.Single());
    }

    [TestMethod]
    public void Compare_NullOrBlankArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortComparison.Compare(null!, Upstream(0, string.Empty), UpstreamSurl, Port(0, string.Empty), PortSurl, SurlCommit));
        Assert.ThrowsExactly<ArgumentException>(() => CurlPortComparison.Compare(Case(), Upstream(0, string.Empty), UpstreamSurl, Port(0, string.Empty), PortSurl, " "));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortComparison.Compare(Case(), null!, UpstreamSurl, Port(0, string.Empty), PortSurl, SurlCommit));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortComparison.Compare(Case(), Upstream(0, string.Empty), UpstreamSurl, null!, PortSurl, SurlCommit));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortComparison.Compare(Case(), Upstream(0, string.Empty), null!, Port(0, string.Empty), PortSurl, SurlCommit));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortComparison.Compare(Case(), Upstream(0, string.Empty), UpstreamSurl, Port(0, string.Empty), null!, SurlCommit));
    }

    internal static CurlPortMeasurementCase Case() => new(
        "Get_HelloTxt",
        "UpstreamCurlFetchesFromSurlOverHttp11Tests.Get_ServesTheFile",
        "http",
        ["-s", "-S", "{surl}/hello.txt"],
        "http",
        [],
        new Dictionary<string, byte[]> { ["hello.txt"] = "hello\n"u8.ToArray() },
        upstream => upstream.ExitCode == 0 ? null : $"upstream exited {upstream.ExitCode}, not 0");

    private static void AssertOnlyNormalisedFieldDiffers(string upstreamOutput, string agreeingPortOutput, string differingPortOutput)
    {
        Assert.AreEqual(
            CurlPortVerdictKind.Agreement,
            Compare(Case(), Upstream(0, upstreamOutput), UpstreamSurl, Port(0, agreeingPortOutput), PortSurl).Kind);
        Assert.AreEqual(
            CurlPortVerdictKind.PortDefect,
            Compare(Case(), Upstream(0, upstreamOutput), UpstreamSurl, Port(0, differingPortOutput), PortSurl).Kind);
    }

    private static CurlPortVerdict Compare(
        CurlPortMeasurementCase measurementCase,
        UpstreamCurlRunResult upstream,
        CurlPortSurlSide upstreamSurl,
        CurlPortClientRunResult port,
        CurlPortSurlSide portSurl) =>
        CurlPortComparison.Compare(measurementCase, upstream, upstreamSurl, port, portSurl, SurlCommit);

    private static UpstreamCurlRunResult Upstream(int exitCode, string output, PinnedUpstreamCurlBuild? build = null, string standardError = "") =>
        UpstreamCurlRunResult.Exited(build ?? ReferenceBuild, exitCode, Encoding.Latin1.GetBytes(output), standardError);

    private static CurlPortClientRunResult Port(int exitCode, string output, string standardError = "") =>
        CurlPortClientRunResult.Exited(PortClient, exitCode, Encoding.Latin1.GetBytes(output), standardError);

    private static IReadOnlyList<byte[]> Transcript(params string[] connections)
    {
        var transcript = new ClientByteTranscript();
        foreach (var connection in connections)
        {
            transcript.Record(transcript.BeginConnection(), Encoding.Latin1.GetBytes(connection));
        }

        return transcript.ToConnections();
    }
}
