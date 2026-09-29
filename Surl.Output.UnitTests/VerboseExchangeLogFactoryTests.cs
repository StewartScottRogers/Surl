using System.Net;
using System.Text;

namespace Surl.Output;

[TestClass]
public sealed class VerboseExchangeLogFactoryTests
{
    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 50000);

    private static string Lines(params string[] lines) =>
        string.Concat(lines.Select(line => line + Environment.NewLine));

    [TestMethod]
    public void BytesReceived_RequestHead_WritesOneReceivedLinePerHeadLine()
    {
        using var writer = new StringWriter();
        var log = new VerboseExchangeLogFactory(writer, verbose: true).Create(1, Remote);

        log.BytesReceived("GET / HTTP/1.1\r\nHost: 127.0.0.1:8080\r\n\r\n"u8);

        Assert.AreEqual(
            Lines("#1 < GET / HTTP/1.1\\r\\n", "#1 < Host: 127.0.0.1:8080\\r\\n", "#1 < \\r\\n"),
            writer.ToString());
    }

    [TestMethod]
    public void BytesSent_StatusLine_WritesGreaterThanLine()
    {
        using var writer = new StringWriter();
        var log = new VerboseExchangeLogFactory(writer, verbose: true).Create(12, Remote);

        log.BytesSent("HTTP/1.1 200 OK\r\n"u8);

        Assert.AreEqual(Lines("#12 > HTTP/1.1 200 OK\\r\\n"), writer.ToString());
    }

    [TestMethod]
    public void BytesSent_TrailingBytesWithoutLineFeed_EndTheLastLine()
    {
        using var writer = new StringWriter();
        var log = new VerboseExchangeLogFactory(writer, verbose: true).Create(1, Remote);

        log.BytesSent("a\nbc"u8);

        Assert.AreEqual(Lines("#1 > a\\n", "#1 > bc"), writer.ToString());
    }

    [TestMethod]
    public void NoteOutsideExchange_Verbose_WritesAStarLineWithADashForTheExchangeId()
    {
        using var writer = new StringWriter();
        var factory = new VerboseExchangeLogFactory(writer, verbose: true);

        factory.NoteOutsideExchange("Refused a connection from 127.0.0.1:50000: past --max-connections 1.");

        Assert.AreEqual(Lines("#- * Refused a connection from 127.0.0.1:50000: past --max-connections 1."), writer.ToString());
    }

    [TestMethod]
    public void NoteOutsideExchange_ControlBytes_AreEscapedLikeAnyNote()
    {
        using var writer = new StringWriter();
        var factory = new VerboseExchangeLogFactory(writer, verbose: true);

        factory.NoteOutsideExchange("a\r\nb");

        Assert.AreEqual(Lines("#- * a\\r\\nb"), writer.ToString());
    }

    [TestMethod]
    public void NoteOutsideExchange_NotVerbose_WritesNothing()
    {
        using var writer = new StringWriter();
        var factory = new VerboseExchangeLogFactory(writer, verbose: false);

        factory.NoteOutsideExchange("Refused");

        Assert.AreEqual(string.Empty, writer.ToString());
    }

    [TestMethod]
    public void NoteOutsideExchange_NullText_Throws()
    {
        var factory = new VerboseExchangeLogFactory(TextWriter.Null, verbose: true);

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => factory.NoteOutsideExchange(null!));

        Assert.AreEqual("text", exception.ParamName);
    }

    [TestMethod]
    public void Note_WritesStarLine()
    {
        using var writer = new StringWriter();
        var log = new VerboseExchangeLogFactory(writer, verbose: true).Create(3, Remote);

        log.Note("Closed");

        Assert.AreEqual(Lines("#3 * Closed"), writer.ToString());
    }

    [TestMethod]
    public void Note_WithCrLfBackslashAndNonAscii_IsOneEscapedLine()
    {
        using var writer = new StringWriter();
        var log = new VerboseExchangeLogFactory(writer, verbose: true).Create(1, Remote);

        log.Note("C:\\a\r\nb\u00E9\u001B");

        Assert.AreEqual(Lines("#1 * C:\\x5Ca\\r\\nb\\xC3\\xA9\\x1B"), writer.ToString());
    }

    [TestMethod]
    public void BytesReceived_CrAndNonPrintableByte_AreEscaped()
    {
        using var writer = new StringWriter();
        var log = new VerboseExchangeLogFactory(writer, verbose: true).Create(1, Remote);

        log.BytesReceived([(byte)'a', (byte)'\r', 0x00, 0x1B, (byte)'[', 0x7F, 0xFF, (byte)'\\']);

        Assert.AreEqual(Lines("#1 < a\\r\\x00\\x1B[\\x7F\\xFF\\x5C"), writer.ToString());
    }

    [TestMethod]
    public void BytesReceived_Empty_WritesNothing()
    {
        using var writer = new StringWriter();
        var log = new VerboseExchangeLogFactory(writer, verbose: true).Create(1, Remote);

        log.BytesReceived([]);

        Assert.AreEqual(string.Empty, writer.ToString());
    }

    [TestMethod]
    public void BytesReceived_MoreThan1024BytesWithoutLineFeed_SplitsEvery1024Bytes()
    {
        using var writer = new StringWriter();
        var log = new VerboseExchangeLogFactory(writer, verbose: true).Create(1, Remote);
        var bytes = Enumerable.Repeat((byte)'a', 2049).ToArray();

        log.BytesReceived(bytes);

        Assert.AreEqual(
            Lines("#1 < " + new string('a', 1024), "#1 < " + new string('a', 1024), "#1 < a"),
            writer.ToString());
    }

    [TestMethod]
    public void BytesReceived_LineFeedAtByte1024_EndsTheLineThere()
    {
        using var writer = new StringWriter();
        var log = new VerboseExchangeLogFactory(writer, verbose: true).Create(1, Remote);
        var bytes = Enumerable.Repeat((byte)'a', 1023).Append((byte)'\n').Append((byte)'b').ToArray();

        log.BytesReceived(bytes);

        Assert.AreEqual(Lines("#1 < " + new string('a', 1023) + "\\n", "#1 < b"), writer.ToString());
    }

    [TestMethod]
    public void VerboseOff_EveryEvent_WritesNothing()
    {
        using var writer = new StringWriter();
        var log = new VerboseExchangeLogFactory(writer, verbose: false).Create(1, Remote);

        log.BytesReceived("GET / HTTP/1.1\r\n"u8);
        log.BytesSent("HTTP/1.1 200 OK\r\n"u8);
        log.Note("Closed");

        Assert.AreEqual(string.Empty, writer.ToString());
    }

    [TestMethod]
    public void VerboseOff_NullNote_Throws()
    {
        using var writer = new StringWriter();
        var log = new VerboseExchangeLogFactory(writer, verbose: false).Create(1, Remote);

        Assert.ThrowsExactly<ArgumentNullException>(() => log.Note(null!));
    }

    [TestMethod]
    public void VerboseOn_NullNote_Throws()
    {
        using var writer = new StringWriter();
        var log = new VerboseExchangeLogFactory(writer, verbose: true).Create(1, Remote);

        Assert.ThrowsExactly<ArgumentNullException>(() => log.Note(null!));
    }

    [TestMethod]
    public void Constructor_NullWriter_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new VerboseExchangeLogFactory(null!, verbose: true));

    [TestMethod]
    public void Create_NullRemoteEndPoint_Throws()
    {
        using var writer = new StringWriter();
        var factory = new VerboseExchangeLogFactory(writer, verbose: true);

        Assert.ThrowsExactly<ArgumentNullException>(() => factory.Create(1, null!));
    }

    [TestMethod]
    public async Task ConcurrentExchanges_NeverInterleaveWithinALine()
    {
        const int exchanges = 16;
        const int eventsPerExchange = 200;
        var writer = new CharByCharWriter();
        var factory = new VerboseExchangeLogFactory(writer, verbose: true);

        await Task.WhenAll(Enumerable.Range(1, exchanges).Select(id => Task.Run(() =>
        {
            var log = factory.Create(id, Remote);
            for (var i = 0; i < eventsPerExchange; i++)
            {
                log.BytesReceived("GET / HTTP/1.1\r\nHost: h\r\n"u8);
                log.Note("note " + id);
            }
        })));

        var lines = writer.ToString().Split(Environment.NewLine);
        Assert.AreEqual(string.Empty, lines[^1]);
        var written = lines[..^1];
        Assert.HasCount(exchanges * eventsPerExchange * 3, written);
        foreach (var line in written)
        {
            var id = line[1..line.IndexOf(' ', StringComparison.Ordinal)];
            var rest = line[(id.Length + 2)..];
            var allowed = new[] { "< GET / HTTP/1.1\\r\\n", "< Host: h\\r\\n", "* note " + id };
            CollectionAssert.Contains(allowed, rest, $"Interleaved line: {line}");
        }
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
