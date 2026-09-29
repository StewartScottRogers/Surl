using System.Net;
using System.Text;

namespace Surl.Output;

/// <summary>
/// What each log level writes (ADR-0033, sections 1 and 3) for one scripted connection and
/// one datagram flow, and the <c>--trace-time</c> stamp (section 5).
/// </summary>
[TestClass]
public sealed class LevelledExchangeLogFactoryTests
{
    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 50000);

    private const string Refused = "Refused a connection from 127.0.0.1:50002: past --max-connections 1.";

    private static string Lines(params string[] lines) =>
        string.Concat(lines.Select(line => line + Environment.NewLine));

    private static LevelledExchangeLogFactory Factory(TextWriter writer, LogLevel level) =>
        new(writer, level, stampTimes: false, TimeProvider.System);

    /// <summary>
    /// A connection that opens, moves bytes, notes a detail and a completed handshake, and
    /// closes; then a datagram flow that is cancelled; then a protocol server that throws;
    /// then a refusal outside any exchange.
    /// </summary>
    private static void ScriptExchanges(Surl.Protocol.Abstractions.IExchangeLogFactory factory)
    {
        var connection = factory.Create(1, Remote);
        connection.Note("Exchange 1 opened: https from 127.0.0.1:50000.");
        connection.Note("TLS handshake completed: Tls13, TLS_AES_256_GCM_SHA384, ALPN none");
        connection.BytesReceived("GET / HTTP/1.1\r\n\r\n"u8);
        connection.Note("Serving /index.html");
        connection.BytesSent("HTTP/1.1 200 OK\r\n\r\n"u8);
        connection.Note("Exchange 1 ended; closing the connection.");

        var flow = factory.Create(7, Remote);
        flow.Note("Exchange 7 opened: tftp from 127.0.0.1:50001.");
        flow.BytesReceived("\0\u0001a\0octet\0"u8);
        flow.Note("Exchange 7 cancelled: no byte moved for the idle timeout of 5 s.");
        flow.Note("Exchange 7 ended; closing the flow.");

        var failing = factory.Create(8, Remote);
        failing.Note("Exchange 8 opened: http from 127.0.0.1:50003.");
        failing.Note("TLS handshake failed: bad record");
        failing.Note("Exchange 8 ended because the protocol server threw InvalidOperationException: boom");

        factory.NoteOutsideExchange(Refused);
    }

    private static string WrittenAt(LogLevel level)
    {
        using var writer = new StringWriter();
        ScriptExchanges(Factory(writer, level));
        return writer.ToString();
    }

    [TestMethod]
    public void None_ScriptedExchanges_WritesNothing() =>
        Assert.AreEqual(string.Empty, WrittenAt(LogLevel.None));

    [TestMethod]
    public void Error_ScriptedExchanges_WritesOnlyTheProtocolServerThrewNote() =>
        Assert.AreEqual(
            Lines("#8 * Exchange 8 ended because the protocol server threw InvalidOperationException: boom"),
            WrittenAt(LogLevel.Error));

    [TestMethod]
    public void Info_ScriptedExchanges_WritesTheOpenLinesAndTheTroubleNotes() =>
        Assert.AreEqual(
            Lines(
                "#1 * Exchange 1 opened: https from 127.0.0.1:50000.",
                "#7 * Exchange 7 opened: tftp from 127.0.0.1:50001.",
                "#7 * Exchange 7 cancelled: no byte moved for the idle timeout of 5 s.",
                "#8 * Exchange 8 opened: http from 127.0.0.1:50003.",
                "#8 * TLS handshake failed: bad record",
                "#8 * Exchange 8 ended because the protocol server threw InvalidOperationException: boom",
                "#- * " + Refused),
            WrittenAt(LogLevel.Info));

    [TestMethod]
    public void Verbose_ScriptedExchanges_WritesEveryEventAsTheVerboseLogDoes()
    {
        using var expectedWriter = new StringWriter();
        ScriptExchanges(new VerboseExchangeLogFactory(expectedWriter, verbose: true));
        var verboseLog = expectedWriter.ToString();

        Assert.AreEqual(verboseLog, WrittenAt(LogLevel.Verbose));
        Assert.AreEqual(
            Lines(
                "#1 * Exchange 1 opened: https from 127.0.0.1:50000.",
                "#1 * TLS handshake completed: Tls13, TLS_AES_256_GCM_SHA384, ALPN none",
                "#1 < GET / HTTP/1.1\\r\\n",
                "#1 < \\r\\n",
                "#1 * Serving /index.html",
                "#1 > HTTP/1.1 200 OK\\r\\n",
                "#1 > \\r\\n",
                "#1 * Exchange 1 ended; closing the connection.",
                "#7 * Exchange 7 opened: tftp from 127.0.0.1:50001.",
                "#7 < \\x00\\x01a\\x00octet\\x00",
                "#7 * Exchange 7 cancelled: no byte moved for the idle timeout of 5 s.",
                "#7 * Exchange 7 ended; closing the flow.",
                "#8 * Exchange 8 opened: http from 127.0.0.1:50003.",
                "#8 * TLS handshake failed: bad record",
                "#8 * Exchange 8 ended because the protocol server threw InvalidOperationException: boom",
                "#- * " + Refused),
            verboseLog);
    }

    [TestMethod]
    public void Info_AnotherExchangesOpenNote_IsNotWritten()
    {
        using var writer = new StringWriter();
        var log = Factory(writer, LogLevel.Info).Create(2, Remote);

        log.Note("Exchange 3 opened: http from 127.0.0.1:50000.");

        Assert.AreEqual(string.Empty, writer.ToString());
    }

    [TestMethod]
    public void Info_OpenNoteWithControlBytes_IsEscaped()
    {
        using var writer = new StringWriter();
        var log = Factory(writer, LogLevel.Info).Create(1, Remote);

        log.Note("Exchange 1 opened: http from \u001B[31m.");

        Assert.AreEqual(Lines("#1 * Exchange 1 opened: http from \\x1B[31m."), writer.ToString());
    }

    [TestMethod]
    [DataRow(LogLevel.None)]
    [DataRow(LogLevel.Error)]
    [DataRow(LogLevel.Info)]
    [DataRow(LogLevel.Verbose)]
    public void Note_NullText_Throws(LogLevel level)
    {
        var log = Factory(TextWriter.Null, level).Create(1, Remote);

        Assert.ThrowsExactly<ArgumentNullException>(() => log.Note(null!));
    }

    [TestMethod]
    public void NoteOutsideExchange_NullText_Throws()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => Factory(TextWriter.Null, LogLevel.None).NoteOutsideExchange(null!));

        Assert.AreEqual("text", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_NullWriter_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Factory(null!, LogLevel.Info));

    [TestMethod]
    public void Constructor_NullTimeProvider_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new LevelledExchangeLogFactory(TextWriter.Null, LogLevel.Info, stampTimes: false, null!));

    [TestMethod]
    [DataRow(LogLevel.Trace)]
    [DataRow((LogLevel)(-1))]
    [DataRow((LogLevel)99)]
    public void Constructor_LevelThisFactoryDoesNotWrite_Throws(LogLevel level)
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Factory(TextWriter.Null, level));

        Assert.AreEqual("level", exception.ParamName);
    }

    [TestMethod]
    public void Create_NullRemoteEndPoint_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Factory(TextWriter.Null, LogLevel.Info).Create(1, null!));

    [TestMethod]
    public void TraceTime_Verbose_StampsEveryLineWithLocalTime()
    {
        using var writer = new StringWriter();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 29, 8, 7, 20, TimeSpan.Zero).AddTicks(8951234), TimeSpan.FromHours(2));
        var factory = new LevelledExchangeLogFactory(writer, LogLevel.Verbose, stampTimes: true, clock);
        var log = factory.Create(1, Remote);

        log.Note("Exchange 1 opened: http from 127.0.0.1:50000.");
        log.BytesReceived("GET / HTTP/1.1\r\n\r\n"u8);
        clock.Advance(TimeSpan.FromHours(3));
        log.BytesSent("HTTP/1.1 200 OK\r\n"u8);
        factory.NoteOutsideExchange(Refused);

        Assert.AreEqual(
            Lines(
                "10:07:20.895123 #1 * Exchange 1 opened: http from 127.0.0.1:50000.",
                "10:07:20.895123 #1 < GET / HTTP/1.1\\r\\n",
                "10:07:20.895123 #1 < \\r\\n",
                "13:07:20.895123 #1 > HTTP/1.1 200 OK\\r\\n",
                "13:07:20.895123 #- * " + Refused),
            writer.ToString());
    }

    [TestMethod]
    public void TraceTime_InfoAndError_StampTheLinesTheyWrite()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 29, 23, 59, 59, TimeSpan.Zero), TimeSpan.Zero);
        using var infoWriter = new StringWriter();
        using var errorWriter = new StringWriter();

        ScriptExchanges(new LevelledExchangeLogFactory(infoWriter, LogLevel.Info, stampTimes: true, clock));
        ScriptExchanges(new LevelledExchangeLogFactory(errorWriter, LogLevel.Error, stampTimes: true, clock));

        var infoLines = infoWriter.ToString().Split(Environment.NewLine)[..^1];
        Assert.HasCount(7, infoLines);
        Assert.IsTrue(infoLines.All(line => line.StartsWith("23:59:59.000000 #", StringComparison.Ordinal)), infoWriter.ToString());
        Assert.AreEqual(
            Lines("23:59:59.000000 #8 * Exchange 8 ended because the protocol server threw InvalidOperationException: boom"),
            errorWriter.ToString());
    }

    [TestMethod]
    public void TraceTimeOff_DoesNotReadTheClock()
    {
        using var writer = new StringWriter();
        var clock = new FixedTimeProvider(DateTimeOffset.UnixEpoch, TimeSpan.Zero);

        ScriptExchanges(new LevelledExchangeLogFactory(writer, LogLevel.Verbose, stampTimes: false, clock));

        Assert.AreEqual(0, clock.Reads);
        Assert.StartsWith("#1 * ", writer.ToString());
    }

    [TestMethod]
    public async Task Info_ConcurrentExchanges_NeverInterleaveWithinALine()
    {
        const int exchanges = 16;
        const int eventsPerExchange = 200;
        var writer = new CharByCharWriter();
        var factory = Factory(writer, LogLevel.Info);

        await Task.WhenAll(Enumerable.Range(1, exchanges).Select(id => Task.Run(() =>
        {
            var log = factory.Create(id, Remote);
            for (var i = 0; i < eventsPerExchange; i++)
            {
                log.Note($"Exchange {id} opened: http from 127.0.0.1:50000.");
                log.BytesReceived("GET / HTTP/1.1\r\n"u8);
                log.Note($"Exchange {id} cancelled at shutdown.");
            }
        })));

        var lines = writer.ToString().Split(Environment.NewLine);
        Assert.AreEqual(string.Empty, lines[^1]);
        var written = lines[..^1];
        Assert.HasCount(exchanges * eventsPerExchange * 2, written);
        foreach (var line in written)
        {
            var id = line[1..line.IndexOf(' ', StringComparison.Ordinal)];
            var allowed = new[]
            {
                $"#{id} * Exchange {id} opened: http from 127.0.0.1:50000.",
                $"#{id} * Exchange {id} cancelled at shutdown.",
            };
            CollectionAssert.Contains(allowed, line, $"Interleaved line: {line}");
        }
    }

    /// <summary>
    /// A clock stopped at a given instant, in a fixed time zone, that counts its reads.
    /// </summary>
    private sealed class FixedTimeProvider(DateTimeOffset utcNow, TimeSpan utcOffset) : TimeProvider
    {
        private DateTimeOffset now = utcNow;

        public int Reads { get; private set; }

        public override TimeZoneInfo LocalTimeZone { get; } =
            TimeZoneInfo.CreateCustomTimeZone("Surl test zone", utcOffset, "Surl test zone", "Surl test zone");

        public override DateTimeOffset GetUtcNow()
        {
            Reads++;
            return now;
        }

        public void Advance(TimeSpan by) => now += by;
    }

    /// <summary>
    /// A writer that writes a string one character at a time and yields between them, so
    /// two unsynchronised writers would mix their characters.
    /// </summary>
    private sealed class CharByCharWriter : TextWriter
    {
        private readonly StringBuilder text = new();
        private readonly Lock textLock = new();

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            lock (textLock)
            {
                text.Append(value);
            }

            Thread.Yield();
        }

        public override void Write(string? value)
        {
            foreach (var character in value ?? string.Empty)
            {
                Write(character);
            }
        }

        public override string ToString()
        {
            lock (textLock)
            {
                return text.ToString();
            }
        }
    }
}
