using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Phase 7 for HTTP/2 (ADR-0089, BL-589): every conversation <see cref="UpstreamCurlFetchesFromSurlOverHttp2Tests"/>
/// runs - prior knowledge and the <c>h2c</c> Upgrade on <c>http</c>, ALPN <c>h2</c> on <c>https</c> - through the
/// static 8.21.0 build that test runs on Windows (ADR-0081 decision 1) and through the pinned Curl port, each
/// against its own fresh <c>surl</c>, judged by <see cref="PinnedCurlPortBesideUpstreamCurl"/> and held to
/// <c>Documentation/Conformance/CurlPort/Http2.md</c> by <see cref="CurlPortFindingsFile"/>. One test per
/// mirrored test, of the same name, each naming it in its <see cref="DescriptionAttribute"/>. Upstream curl is
/// right; the port is the thing measured (ADR-0003 decision 6). Inconclusive where either pinned binary is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortBesideUpstreamCurlOverHttp2Tests
{
    private const string FindingsFile = "Http2.md";

    private const string Mirrored = nameof(UpstreamCurlFetchesFromSurlOverHttp2Tests) + ".";

    // stunnel/static-curl's build of upstream curl's tag 8.21.0: the Windows build the mirrored class
    // measures HTTP/2 with (ADR-0081, decision 1), since the reference build has no HTTP2 feature.
    private const string Http2WindowsBuildSha256 = "589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648";

    // curl's -w variable for the HTTP version it spoke: "2" for HTTP/2.
    private const string HttpVersion = "%{http_version}";

    // The two -v lines the mirrored test counts and looks for.
    private const string EstablishedConnection = "* Established connection to ";

    private const string ReusedConnection = "* Reusing existing https: connection with host 127.0.0.1";

    private static readonly byte[] Hello = "hello from surl\n"u8.ToArray();

    // Four times the default initial window of 65,535 bytes (RFC 9113, section 6.9.2), as the mirrored test serves.
    private static readonly byte[] Large = [.. Enumerable.Range(0, 262_144).Select(index => (byte)(index % 251))];

    private static readonly string Credentials = $"{AccountsFile.User}:{AccountsFile.Password}";

    private static readonly string[] SelfSigned = ["--self-signed"];

    private IsolatedCurlHome localFiles = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateLocalFiles() => localFiles = new IsolatedCurlHome();

    [TestCleanup]
    public void DeleteLocalFiles() => localFiles.Dispose();

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithPriorKnowledge_File_IsServedOverHttp2")]
    public Task GetWithPriorKnowledge_File_IsServedOverHttp2() =>
        MeasureAsync(Case("http", [], ["-sS", "--http2-prior-knowledge", "-w", HttpVersion, "{surl}/hello.txt"], ServedOverHttp2(Hello)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithHttp2_File_IsUpgradedToHttp2")]
    public Task GetWithHttp2_File_IsUpgradedToHttp2() =>
        MeasureAsync(Case("http", [], ["-sS", "--http2", "-w", HttpVersion, "{surl}/hello.txt"], ServedOverHttp2(Hello)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithHttp2AndInclude_File_ShowsThe101ThenTheHttp2Head")]
    public Task GetWithHttp2AndInclude_File_ShowsThe101ThenTheHttp2Head() =>
        MeasureAsync(Case("http", [], ["-sS", "-i", "--http2", "{surl}/hello.txt"], run =>
            Exits(0)(run) ?? OutputIs(
                run,
                output => output.StartsWith("HTTP/1.1 101 Switching Protocols\r\n", StringComparison.Ordinal)
                    && output.Contains("\r\n\r\nHTTP/2 200 \r\n", StringComparison.Ordinal)
                    && output.EndsWith("\r\n\r\nhello from surl\n", StringComparison.Ordinal))));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp2Tests.Get_HttpsByAlpn_IsServedOverHttp2")]
    public Task Get_HttpsByAlpn_IsServedOverHttp2() =>
        MeasureAsync(Case("https", SelfSigned, ["-sS", "-k", "-w", HttpVersion, "{surl}/hello.txt"], ServedOverHttp2(Hello)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp2Tests.Head_HttpsByAlpn_ExitsZeroWithAnHttp2OkHead")]
    public Task Head_HttpsByAlpn_ExitsZeroWithAnHttp2OkHead() =>
        MeasureAsync(Case("https", SelfSigned, ["-sS", "-k", "-I", "{surl}/hello.txt"], run =>
            Exits(0)(run) ?? OutputIs(
                run,
                head => head.StartsWith("HTTP/2 200 \r\n", StringComparison.Ordinal)
                    && head.Contains($"content-length: {Hello.Length}\r\n", StringComparison.Ordinal)
                    && head.EndsWith("\r\n\r\n", StringComparison.Ordinal))));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithRange_HttpsByAlpn_IsServedTheFirstTenBytesWith206OverHttp2")]
    public Task GetWithRange_HttpsByAlpn_IsServedTheFirstTenBytesWith206OverHttp2() =>
        MeasureAsync(Case(
            "https",
            SelfSigned,
            ["-sS", "-k", "-r", "0-9", "-w", " %{response_code} " + HttpVersion, "{surl}/hello.txt"],
            ExitsZeroWriting([.. Hello.Take(10), .. " 206 2"u8.ToArray()])));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithBasic_HttpsByAlpn_IsServedOverHttp2")]
    public async Task GetWithBasic_HttpsByAlpn_IsServedOverHttp2()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            "https",
            ["--self-signed", "--user-file", accounts.Path],
            ["-sS", "-f", "-k", "--basic", "-u", Credentials, "-w", HttpVersion, "{surl}/hello.txt"],
            ServedOverHttp2(Hello)));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithBasic_PriorKnowledgeAllowPlaintextAuth_IsServedOverHttp2")]
    public async Task GetWithBasic_PriorKnowledgeAllowPlaintextAuth_IsServedOverHttp2()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            "http",
            ["--allow-plaintext-auth", "--user-file", accounts.Path],
            ["-sS", "-f", "--http2-prior-knowledge", "--basic", "-u", Credentials, "-w", HttpVersion, "{surl}/hello.txt"],
            ServedOverHttp2(Hello)));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithBasic_WrongPassword_IsRefusedWith401OverHttp2")]
    public async Task GetWithBasic_WrongPassword_IsRefusedWith401OverHttp2()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            "https",
            ["--self-signed", "--user-file", accounts.Path],
            ["-sS", "-k", "--basic", "-u", $"{AccountsFile.User}:wrong", "-o", NullDevice, "-w", "%{response_code} " + HttpVersion, "{surl}/hello.txt"],
            ExitsZeroWriting("401 2"u8.ToArray())));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithDigest_PriorKnowledge_IsServedOverHttp2")]
    public async Task GetWithDigest_PriorKnowledge_IsServedOverHttp2()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            "http",
            ["--user-file", accounts.Path],
            ["-sS", "-f", "--http2-prior-knowledge", "--digest", "-u", Credentials, "-w", HttpVersion, "{surl}/hello.txt"],
            ServedOverHttp2(Hello)));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithDigest_HttpsByAlpn_IsServedOverHttp2")]
    public async Task GetWithDigest_HttpsByAlpn_IsServedOverHttp2()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            "https",
            ["--self-signed", "--user-file", accounts.Path],
            ["-sS", "-f", "-k", "--digest", "-u", Credentials, "-w", HttpVersion, "{surl}/hello.txt"],
            ServedOverHttp2(Hello)));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp2Tests.Upload_AllowUploads_IsStoredWith201OverHttp2")]
    public async Task Upload_AllowUploads_IsStoredWith201OverHttp2()
    {
        var upload = await localFiles.WriteFileAsync("up.bin", Large, TestContext.CancellationToken);
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            "https",
            ["--self-signed", "--allow-uploads", "--user-file", accounts.Path],
            ["-sS", "-k", "-u", Credentials, "-T", upload, "-o", NullDevice, "-w", "%{response_code} " + HttpVersion, "{surl}/up.bin"],
            run => ExitsZeroWriting("201 2"u8.ToArray())(run) ?? Stored(run, "up.bin", Large)) with
        {
            ComparedStoredFiles = ["up.bin"],
        });
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp2Tests.Get_FileLargerThanTheInitialWindow_IsServedWholeOverHttp2")]
    public Task Get_FileLargerThanTheInitialWindow_IsServedWholeOverHttp2() =>
        MeasureAsync(Case("https", SelfSigned, ["-sS", "-k", "-w", HttpVersion, "{surl}/large.bin"], ServedOverHttp2(Large)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp2Tests.GetWithPriorKnowledge_FileLargerThanTheInitialWindow_IsServedWholeOverHttp2")]
    public Task GetWithPriorKnowledge_FileLargerThanTheInitialWindow_IsServedWholeOverHttp2() =>
        MeasureAsync(Case("http", [], ["-sS", "--http2-prior-knowledge", "-w", HttpVersion, "{surl}/large.bin"], ServedOverHttp2(Large)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp2Tests.Get_TwoUrls_ReuseOneHttp2Connection")]
    public Task Get_TwoUrls_ReuseOneHttp2Connection() =>
        MeasureAsync(Case(
            "https",
            SelfSigned,
            ["-sS", "-k", "-v", "-w", HttpVersion, "{surl}/hello.txt", "{surl}/hello.txt"],
            run => ExitsZeroWriting([.. Hello, .. "2"u8.ToArray(), .. Hello, .. "2"u8.ToArray()])(run)
                ?? (CountOf(run.StandardError, EstablishedConnection) == 1 && run.StandardError.Contains(ReusedConnection, StringComparison.Ordinal)
                    ? null
                    : $"not one connection reused: {run.StandardError}")) with
        {
            // Only the reuse line is compared: the "Established connection" line names the client's own
            // ephemeral port, which the normaliser leaves, so it differs between any two runs.
            AssertedStandardErrorFragments = [ReusedConnection],
        });

    // --- The case, the mirrored assertions and the judgement ---

    private static string NullDevice => OperatingSystem.IsWindows() ? "NUL" : "/dev/null";

    /// <summary>
    /// A case as the mirrored test runs it, named after the calling test, against the <c>surl</c>
    /// <paramref name="scheme"/> listener serving <c>hello.txt</c> and <c>large.bin</c>.
    /// </summary>
    private static CurlPortMeasurementCase Case(
        string scheme,
        IReadOnlyList<string> surlOptions,
        IReadOnlyList<string> arguments,
        Func<CurlPortMeasuredRun, string?> check,
        [CallerMemberName] string testName = "") =>
        new(
            testName,
            Mirrored + testName,
            scheme,
            arguments,
            scheme,
            surlOptions,
            new Dictionary<string, byte[]> { ["hello.txt"] = Hello, ["large.bin"] = Large },
            check);

    private static string Describe(CurlPortMeasuredRun run) =>
        $"exit {run.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "timeout"}, {run.StandardOutput.Length} bytes out, stderr: {run.StandardError.Trim()}";

    private static Func<CurlPortMeasuredRun, string?> Exits(int exitCode) =>
        run => run.ExitCode == exitCode ? null : Describe(run);

    private static Func<CurlPortMeasuredRun, string?> ExitsZeroWriting(byte[] expected) =>
        run => run.ExitCode == 0 && run.StandardOutput.AsSpan().SequenceEqual(expected) ? null : Describe(run);

    private static Func<CurlPortMeasuredRun, string?> ServedOverHttp2(byte[] body) =>
        ExitsZeroWriting([.. body, .. "2"u8.ToArray()]);

    private static string? OutputIs(CurlPortMeasuredRun run, Func<string, bool> isExpected)
    {
        var output = Encoding.ASCII.GetString(run.StandardOutput);
        return isExpected(output) ? null : $"unexpected output: {output}";
    }

    private static string? Stored(CurlPortMeasuredRun run, string name, byte[] expected) =>
        run.Surl.StoredFiles.TryGetValue(name, out var stored) && stored.AsSpan().SequenceEqual(expected)
            ? null
            : $"{name} stored as {(stored is null ? "nothing" : $"{stored.Length} other bytes")}";

    private static int CountOf(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;

    /// <summary>
    /// Runs the case through both clients and applies ADR-0089 decision 5. On <c>https</c> the client's bytes
    /// are TLS's and are not compared (decision 4). On <c>http</c> they are compared as
    /// <see cref="DecodedRequestFrames"/>, not byte for byte: HPACK Huffman-codes <c>:authority</c>, which
    /// holds each run's own relay port, so the raw bytes of two runs of one client differ (ADR-0089
    /// amendment 2). A difference there is a port defect like any other.
    /// </summary>
    private async Task MeasureAsync(CurlPortMeasurementCase measurementCase)
    {
        var setup = new CurlPortRunSetup { UpstreamBuildSha256 = Http2WindowsBuildSha256 };
        var relays = new List<(ClientBytesRelay Relay, int Port)>();
        if (measurementCase.SurlScheme == "https")
        {
            setup = setup with { ComparedNotes = CurlPortRunSetup.WithoutTlsEndings };
        }
        else
        {
            measurementCase = measurementCase with { RecordsClientBytes = false };
            setup = setup with
            {
                StartFrontRelay = (listener, cancellationToken) =>
                {
                    var relay = ClientBytesRelay.Start(listener, cancellationToken);
                    relays.Add((relay, relay.Port));
                    return (relay, relay.BaseUrlFor(listener));
                },
            };
        }

        var measurement = await PinnedCurlPortBesideUpstreamCurl.MeasureAsync(TestContext, measurementCase, setup);
        var framesDiffer = relays.Count == 2 && FramesDiffer(DecodedRequestFrames(relays[0]), DecodedRequestFrames(relays[1]));
        await AssertVerdictIsListedAsync(measurementCase.Name, measurement, framesDiffer);
    }

    private bool FramesDiffer(string upstream, string port)
    {
        TestContext.WriteLine($"--- upstream's decoded request frames:\n{upstream}\n--- the port's decoded request frames:\n{port}");
        return upstream != port;
    }

    // ADR-0089 decision 5, with the decoded frames as one more compared item.
    private async Task AssertVerdictIsListedAsync(string caseName, PinnedCurlPortMeasurement measurement, bool framesDiffer)
    {
        var verdict = measurement.Verdict;
        if (verdict.Kind != CurlPortVerdictKind.Agreement || !framesDiffer)
        {
            await CurlPortFindingsFile.AssertVerdictIsListedAsync(TestContext, FindingsFile, caseName, measurement);
            return;
        }

        var path = Path.Combine(PinnedUpstreamCurl.RepositoryRoot(), "Documentation", "Conformance", "CurlPort", FindingsFile);
        var listed = CurlPortFindingsFile.ListedCaseNames(await File.ReadAllLinesAsync(path, TestContext.CancellationToken));
        Assert.Contains(caseName, listed, $"A port defect {FindingsFile} does not list; add it (ADR-0089 decision 6): the decoded request frames differ.");
    }

    /// <summary>
    /// What the client sent on each connection through <paramref name="relay"/>, one line per item: an
    /// HTTP/1.1 request head (the <c>h2c</c> Upgrade) line by line, then one line per HTTP/2 frame after the
    /// connection preface, its type, flags and stream, with a <c>SETTINGS</c> frame's parameters, a
    /// <c>HEADERS</c> block's decoded fields (RFC 7541) and a <c>DATA</c> frame's length. Normalised as
    /// decision 4 normalises request bytes. Not rendered: every <c>WINDOW_UPDATE</c> but the
    /// connection's first, since how many follow, and where the <c>SETTINGS</c> acknowledgement falls among
    /// them, depend on how the client happened to read the answer (timing, which decision 4 never
    /// compares); and a <c>PING</c>'s opaque data.
    /// </summary>
    private static string DecodedRequestFrames((ClientBytesRelay Relay, int Port) relay)
    {
        var lines = new StringBuilder();
        foreach (var (connection, index) in relay.Relay.Transcript.ToConnections().Select((bytes, index) => (bytes, index)))
        {
            lines.Append(CultureInfo.InvariantCulture, $"connection {index}\n");
            var text = Encoding.Latin1.GetString(CurlPortResultNormaliser.NormaliseRequestBytes(RenderConnection(connection), relay.Port));
            lines.Append(text);
        }

        return lines.ToString();
    }

    private static byte[] RenderConnection(byte[] connection)
    {
        var preface = "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8;
        var start = connection.AsSpan().IndexOf(preface);
        if (start < 0)
        {
            return connection;
        }

        var rendered = new StringBuilder(Encoding.Latin1.GetString(connection, 0, start));
        rendered.Append("<preface>\n");
        RenderFrames(connection.AsSpan(start + preface.Length), rendered);
        return Encoding.Latin1.GetBytes(rendered.ToString());
    }

    private static void RenderFrames(ReadOnlySpan<byte> frames, StringBuilder rendered)
    {
        var decoder = new Surl.Hpack.HpackDecoder(Surl.Hpack.HpackDecoder.DefaultMaximumTableSize, int.MaxValue);
        var block = new List<byte>();
        var connectionWindowSeen = false;
        while (frames.Length >= 9)
        {
            var length = (frames[0] << 16) | (frames[1] << 8) | frames[2];
            var type = frames[3];
            var flags = frames[4];
            var stream = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(frames[5..]) & int.MaxValue;
            var payload = frames.Slice(9, Math.Min(length, frames.Length - 9));
            frames = frames[(9 + payload.Length)..];
            if (type == 0x8 && (stream != 0 || connectionWindowSeen))
            {
                continue;
            }

            connectionWindowSeen |= type == 0x8;

            rendered.Append(CultureInfo.InvariantCulture, $"{FrameTypeName(type)} flags=0x{flags:X2} stream={stream}");
            if (type is 0x1 or 0x9)
            {
                block.AddRange(HeaderBlockFragment(type, flags, payload).ToArray());
                if ((flags & 0x4) != 0)
                {
                    rendered.Append(RenderHeaderBlock(decoder, [.. block]));
                    block.Clear();
                }
            }
            else
            {
                rendered.Append(RenderPayload(type, payload));
            }

            rendered.Append('\n');
        }

        if (!frames.IsEmpty)
        {
            rendered.Append(CultureInfo.InvariantCulture, $"<{frames.Length} bytes of a partial frame>\n");
        }
    }

    private static string FrameTypeName(byte type) => type switch
    {
        0x0 => "DATA",
        0x1 => "HEADERS",
        0x2 => "PRIORITY",
        0x3 => "RST_STREAM",
        0x4 => "SETTINGS",
        0x5 => "PUSH_PROMISE",
        0x6 => "PING",
        0x7 => "GOAWAY",
        0x8 => "WINDOW_UPDATE",
        0x9 => "CONTINUATION",
        _ => $"type 0x{type:X2}",
    };

    // RFC 9113 sections 6.2 and 6.10: a HEADERS frame's fragment without its padding and priority.
    private static ReadOnlySpan<byte> HeaderBlockFragment(byte type, byte flags, ReadOnlySpan<byte> payload)
    {
        if (type != 0x1)
        {
            return payload;
        }

        var padding = 0;
        if ((flags & 0x8) != 0 && payload.Length > 0)
        {
            padding = payload[0];
            payload = payload[1..];
        }

        if ((flags & 0x20) != 0 && payload.Length >= 5)
        {
            payload = payload[5..];
        }

        return payload[..Math.Max(0, payload.Length - padding)];
    }

    private static string RenderHeaderBlock(Surl.Hpack.HpackDecoder decoder, byte[] block)
    {
        var result = decoder.Decode(block);
        if (!result.IsDecoded)
        {
            return $" <undecodable header block: {result.Error}>";
        }

        return string.Concat(result.Fields.Select(field => $"\n{field.Name}: {field.Value}"));
    }

    private static string RenderPayload(byte type, ReadOnlySpan<byte> payload)
    {
        switch (type)
        {
            case 0x0:
                return $" length={payload.Length}";
            case 0x4:
                var settings = new StringBuilder();
                for (var offset = 0; offset + 6 <= payload.Length; offset += 6)
                {
                    settings.Append(CultureInfo.InvariantCulture, $" 0x{(payload[offset] << 8) | payload[offset + 1]:X2}={System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(payload[(offset + 2)..])}");
                }

                return settings.ToString();
            case 0x6:
                return string.Empty;
            default:
                return " " + Convert.ToHexString(payload);
        }
    }
}
