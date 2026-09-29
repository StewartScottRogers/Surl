using System.Globalization;
using System.Net;
using System.Text;

namespace Surl.Output;

/// <summary>
/// The <c>--trace</c> and <c>--trace-ascii</c> dumps (ADR-0033, section 4), in the layout
/// measured from pinned upstream curl 8.21.0, and their <c>--trace-time</c> stamps
/// (section 5).
/// </summary>
[TestClass]
public sealed class TraceExchangeLogFactoryTests
{
    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 50000);

    /// <summary>
    /// <c>Surl.Protocol.Http.UnitTests/Fixtures/get-file/request.bin</c>: the request pinned
    /// upstream curl 8.21.0 sent for <c>http://127.0.0.1:18050/file.txt</c>, 87 bytes.
    /// </summary>
    private static ReadOnlySpan<byte> GetFileRequest =>
        "GET /file.txt HTTP/1.1\r\nHost: 127.0.0.1:18050\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n"u8;

    private static ReadOnlySpan<byte> ShortResponse =>
        "HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\nHello\n"u8;

    private static string Lines(params string[] lines) =>
        string.Concat(lines.Select(line => line + Environment.NewLine));

    private static TraceExchangeLogFactory Factory(TextWriter writer, TraceDumpLayout layout) =>
        new(writer, layout, stampTimes: false, TimeProvider.System);

    private static string Dumped(TraceDumpLayout layout, Action<Surl.Protocol.Abstractions.IExchangeLog> events)
    {
        using var writer = new StringWriter();
        events(Factory(writer, layout).Create(1, Remote));
        return writer.ToString();
    }

    private static void ScriptGetFileExchange(Surl.Protocol.Abstractions.IExchangeLog log)
    {
        log.Note("Exchange 1 opened: http from 127.0.0.1:50000.");
        log.BytesReceived(GetFileRequest);
        log.BytesSent(ShortResponse);
        log.Note("Exchange 1 ended; closing the connection.");
    }

    [TestMethod]
    public void GetFileFixture_Trace_DumpsHexAndAsciiRows() =>
        Assert.AreEqual(
            Lines(
                "#1 * Exchange 1 opened: http from 127.0.0.1:50000.",
                "#1 <= Recv data, 87 bytes (0x57)",
                "0000: 47 45 54 20 2f 66 69 6c 65 2e 74 78 74 20 48 54 GET /file.txt HT",
                "0010: 54 50 2f 31 2e 31 0d 0a 48 6f 73 74 3a 20 31 32 TP/1.1..Host: 12",
                "0020: 37 2e 30 2e 30 2e 31 3a 31 38 30 35 30 0d 0a 55 7.0.0.1:18050..U",
                "0030: 73 65 72 2d 41 67 65 6e 74 3a 20 63 75 72 6c 2f ser-Agent: curl/",
                "0040: 38 2e 32 31 2e 30 0d 0a 41 63 63 65 70 74 3a 20 8.21.0..Accept: ",
                "0050: 2a 2f 2a 0d 0a 0d 0a " + new string(' ', 9 * 3) + "*/*....",
                "#1 => Send data, 44 bytes (0x2c)",
                "0000: 48 54 54 50 2f 31 2e 31 20 32 30 30 20 4f 4b 0d HTTP/1.1 200 OK.",
                "0010: 0a 43 6f 6e 74 65 6e 74 2d 4c 65 6e 67 74 68 3a .Content-Length:",
                "0020: 20 36 0d 0a 0d 0a 48 65 6c 6c 6f 0a " + new string(' ', 4 * 3) + " 6....Hello.",
                "#1 * Exchange 1 ended; closing the connection."),
            Dumped(TraceDumpLayout.HexAndAscii, ScriptGetFileExchange));

    [TestMethod]
    public void GetFileFixture_TraceAscii_DumpsRowsEndingAtEachCrLf() =>
        Assert.AreEqual(
            Lines(
                "#1 * Exchange 1 opened: http from 127.0.0.1:50000.",
                "#1 <= Recv data, 87 bytes (0x57)",
                "0000: GET /file.txt HTTP/1.1",
                "0018: Host: 127.0.0.1:18050",
                "002f: User-Agent: curl/8.21.0",
                "0048: Accept: */*",
                "0055: ",
                "#1 => Send data, 44 bytes (0x2c)",
                "0000: HTTP/1.1 200 OK",
                "0011: Content-Length: 6",
                "0024: ",
                "0026: Hello.",
                "#1 * Exchange 1 ended; closing the connection."),
            Dumped(TraceDumpLayout.Ascii, ScriptGetFileExchange));

    /// <summary>
    /// The 79-byte request event of ADR-0033's measured <c>--trace</c> and
    /// <c>--trace-ascii</c> files, whose rows surl writes exactly as upstream curl wrote them.
    /// </summary>
    [TestMethod]
    [DataRow(
        TraceDumpLayout.HexAndAscii,
        new[]
        {
            "0000: 47 45 54 20 2f 20 48 54 54 50 2f 31 2e 31 0d 0a GET / HTTP/1.1..",
            "0010: 48 6f 73 74 3a 20 31 32 37 2e 30 2e 30 2e 31 3a Host: 127.0.0.1:",
            "0020: 31 38 31 30 31 0d 0a 55 73 65 72 2d 41 67 65 6e 18101..User-Agen",
            "0030: 74 3a 20 63 75 72 6c 2f 38 2e 32 31 2e 30 0d 0a t: curl/8.21.0..",
            "0040: 41 63 63 65 70 74 3a 20 2a 2f 2a 0d 0a 0d 0a    Accept: */*....",
        })]
    [DataRow(
        TraceDumpLayout.Ascii,
        new[]
        {
            "0000: GET / HTTP/1.1",
            "0010: Host: 127.0.0.1:18101",
            "0027: User-Agent: curl/8.21.0",
            "0040: Accept: */*",
            "004d: ",
        })]
    public void UpstreamCurlsMeasuredRequest_RowsMatchTheMeasuredDump(TraceDumpLayout layout, string[] rows) =>
        Assert.AreEqual(
            Lines(["#1 <= Recv data, 79 bytes (0x4f)", .. rows]),
            Dumped(layout, log => log.BytesReceived("GET / HTTP/1.1\r\nHost: 127.0.0.1:18101\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n"u8)));

    [TestMethod]
    [DataRow(TraceDumpLayout.HexAndAscii)]
    [DataRow(TraceDumpLayout.Ascii)]
    public void EmptyCalls_WriteNothing(TraceDumpLayout layout) =>
        Assert.AreEqual(
            string.Empty,
            Dumped(layout, log =>
            {
                log.BytesReceived([]);
                log.BytesSent([]);
            }));

    [TestMethod]
    public void TraceAscii_ChunkLongerThanOneRow_BreaksEvery64Bytes() =>
        Assert.AreEqual(
            Lines(
                "#1 => Send data, 130 bytes (0x82)",
                "0000: " + new string('a', 64),
                "0040: " + new string('b', 64),
                "0080: cc"),
            Dumped(TraceDumpLayout.Ascii, log => log.BytesSent(Encoding.ASCII.GetBytes(new string('a', 64) + new string('b', 64) + "cc"))));

    [TestMethod]
    [DataRow(64, "\r\nz", "0000: {0}|0042: z", DisplayName = "CR LF right after a full row ends it and is skipped")]
    [DataRow(63, "\r\nz", "0000: {0}|0041: z", DisplayName = "CR LF at the last place in a row ends it")]
    [DataRow(65, "\r\nz", "0000: {0}|0040: x|0043: z", DisplayName = "CR LF past a full row belongs to the next")]
    [DataRow(1, "\r\r\n\n", "0000: {0}.|0004: .", DisplayName = "A lone CR and a lone LF print as dots")]
    [DataRow(0, "\r\n", "0000: ", DisplayName = "A lone CR LF prints an empty row")]
    public void TraceAscii_CrLf_EndsTheRowUnprinted(int leadingBytes, string tail, string expectedRows)
    {
        var lead = new string('x', leadingBytes);
        var bytes = Encoding.ASCII.GetBytes(lead + tail);

        var dump = Dumped(TraceDumpLayout.Ascii, log => log.BytesReceived(bytes));

        var rows = string.Format(CultureInfo.InvariantCulture, expectedRows, lead[..Math.Min(leadingBytes, 64)]).Split('|');
        Assert.AreEqual(
            Lines([string.Create(CultureInfo.InvariantCulture, $"#1 <= Recv data, {bytes.Length} bytes (0x{bytes.Length:x})"), .. rows]),
            dump);
    }

    [TestMethod]
    [DataRow(TraceDumpLayout.HexAndAscii, "0000: 00 1f 20 7e 7f 80 ff 5c                         .. ~...\\")]
    [DataRow(TraceDumpLayout.Ascii, "0000: .. ~...\\")]
    public void ByteOutsidePrintableAscii_PrintsAsADot(TraceDumpLayout layout, string row) =>
        Assert.AreEqual(
            Lines("#1 <= Recv data, 8 bytes (0x8)", row),
            Dumped(layout, log => log.BytesReceived([0x00, 0x1F, 0x20, 0x7E, 0x7F, 0x80, 0xFF, (byte)'\\'])));

    [TestMethod]
    public void Notes_AreTheVerboseLogsEscapedNoteLines()
    {
        using var writer = new StringWriter();
        var factory = Factory(writer, TraceDumpLayout.Ascii);

        factory.Create(42, Remote).Note("TLS handshake failed: bad\r\nrecord \u001B[31m");
        factory.NoteOutsideExchange("Refused a connection from 127.0.0.1:50002: past --max-connections 1.");

        Assert.AreEqual(
            Lines(
                "#42 * TLS handshake failed: bad\\r\\nrecord \\x1B[31m",
                "#- * Refused a connection from 127.0.0.1:50002: past --max-connections 1."),
            writer.ToString());
    }

    [TestMethod]
    public void TraceTime_StampsHeaderAndNoteLinesButNotRows()
    {
        using var writer = new StringWriter();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 29, 8, 7, 20, TimeSpan.Zero).AddTicks(8951234), TimeSpan.FromHours(2));
        var factory = new TraceExchangeLogFactory(writer, TraceDumpLayout.HexAndAscii, stampTimes: true, clock);
        var log = factory.Create(3, Remote);

        log.Note("Exchange 3 opened: http from 127.0.0.1:50000.");
        log.BytesReceived("GET / HTTP/1.1\r\n"u8);
        clock.Advance(TimeSpan.FromHours(14));
        log.BytesSent("HTTP/1.1 204 No Content\r\n"u8);
        factory.NoteOutsideExchange("Refused a flow from 127.0.0.1:50001: past --max-flows 1.");

        Assert.AreEqual(
            Lines(
                "10:07:20.895123 #3 * Exchange 3 opened: http from 127.0.0.1:50000.",
                "10:07:20.895123 #3 <= Recv data, 16 bytes (0x10)",
                "0000: 47 45 54 20 2f 20 48 54 54 50 2f 31 2e 31 0d 0a GET / HTTP/1.1..",
                "00:07:20.895123 #3 => Send data, 25 bytes (0x19)",
                "0000: 48 54 54 50 2f 31 2e 31 20 32 30 34 20 4e 6f 20 HTTP/1.1 204 No ",
                "0010: 43 6f 6e 74 65 6e 74 0d 0a " + new string(' ', 7 * 3) + "Content..",
                "00:07:20.895123 #- * Refused a flow from 127.0.0.1:50001: past --max-flows 1."),
            writer.ToString());
    }

    [TestMethod]
    public void TraceTimeOff_DoesNotReadTheClock()
    {
        using var writer = new StringWriter();
        var clock = new FixedTimeProvider(DateTimeOffset.UnixEpoch, TimeSpan.Zero);
        var factory = new TraceExchangeLogFactory(writer, TraceDumpLayout.Ascii, stampTimes: false, clock);

        ScriptGetFileExchange(factory.Create(1, Remote));
        factory.NoteOutsideExchange("Refused");

        Assert.AreEqual(0, clock.Reads);
        Assert.StartsWith("#1 * ", writer.ToString());
    }

    [TestMethod]
    [DataRow(TraceDumpLayout.HexAndAscii)]
    [DataRow(TraceDumpLayout.Ascii)]
    public async Task ConcurrentExchanges_NeverInterleaveInsideOneEventBlock(TraceDumpLayout layout)
    {
        const int exchanges = 16;
        const int eventsPerExchange = 50;
        var writer = new CharByCharWriter();
        var factory = Factory(writer, layout);

        await Task.WhenAll(Enumerable.Range(1, exchanges).Select(id => Task.Run(() =>
        {
            var log = factory.Create(id, Remote);
            for (var i = 0; i < eventsPerExchange; i++)
            {
                log.BytesReceived(EventBytes(id));
                log.Note($"Exchange {id} note {i}");
            }
        })));

        var lines = writer.ToString().Split(Environment.NewLine);
        Assert.AreEqual(string.Empty, lines[^1]);
        var blocks = 0;
        for (var index = 0; index < lines.Length - 1; index++)
        {
            var line = lines[index];
            var id = int.Parse(line[1..line.IndexOf(' ', StringComparison.Ordinal)], CultureInfo.InvariantCulture);
            if (line.Contains(" * ", StringComparison.Ordinal))
            {
                Assert.StartsWith($"#{id} * Exchange {id} note ", line);
                continue;
            }

            var expected = Dumped(layout, log => log.BytesReceived(EventBytes(id))).Replace("#1 ", $"#{id} ", StringComparison.Ordinal);
            var expectedLines = expected.Split(Environment.NewLine)[..^1];
            CollectionAssert.AreEqual(expectedLines, lines[index..(index + expectedLines.Length)], $"Interleaved block at line {index}");
            index += expectedLines.Length - 1;
            blocks++;
        }

        Assert.AreEqual(exchanges * eventsPerExchange, blocks);
    }

    /// <summary>
    /// Three rows' worth of bytes that name their exchange, with a CR LF in the middle.
    /// </summary>
    private static byte[] EventBytes(int id) =>
        Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"exchange {id:D2} says hello\r\nand {id:D2} again, at length"));

    [TestMethod]
    public void Constructor_NullWriter_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Factory(null!, TraceDumpLayout.HexAndAscii));

    [TestMethod]
    public void Constructor_NullTimeProvider_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new TraceExchangeLogFactory(TextWriter.Null, TraceDumpLayout.Ascii, stampTimes: false, null!));

    [TestMethod]
    [DataRow((TraceDumpLayout)(-1))]
    [DataRow((TraceDumpLayout)2)]
    public void Constructor_NoSuchLayout_Throws(TraceDumpLayout layout)
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Factory(TextWriter.Null, layout));

        Assert.AreEqual("layout", exception.ParamName);
    }

    [TestMethod]
    public void Create_NullRemoteEndPoint_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Factory(TextWriter.Null, TraceDumpLayout.Ascii).Create(1, null!));

    [TestMethod]
    public void Note_NullText_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Factory(TextWriter.Null, TraceDumpLayout.Ascii).Create(1, Remote).Note(null!));

    [TestMethod]
    public void NoteOutsideExchange_NullText_Throws()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => Factory(TextWriter.Null, TraceDumpLayout.Ascii).NoteOutsideExchange(null!));

        Assert.AreEqual("text", exception.ParamName);
    }
}
