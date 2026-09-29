using System.Globalization;
using System.Net;

namespace Surl.Output;

/// <summary>
/// The verbose log's escaping rule (ADR-0006, section 3), checked for every byte value,
/// reversed by a decoder local to these tests, and shown to cover bytes received, bytes
/// sent and note text alike.
/// </summary>
[TestClass]
public sealed class VerboseLogEscapingTests
{
    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 50000);

    [TestMethod]
    public void EveryByte_RendersByTheAdr0006Rule()
    {
        for (var value = 0; value <= 0xFF; value++)
        {
            Assert.AreEqual(
                ExpectedByAdr0006((byte)value),
                ExchangeLogEscaping.Escape([(byte)value]),
                $"Byte 0x{value:X2}");
        }
    }

    [TestMethod]
    [DataRow(new byte[] { 0x1B, (byte)'[', (byte)'2', (byte)'J' }, "\\x1B[2J", DisplayName = "ESC [ 2 J")]
    [DataRow(new byte[] { (byte)'\\' }, "\\x5C", DisplayName = "Backslash")]
    [DataRow(new byte[] { 0x7F }, "\\x7F", DisplayName = "DEL")]
    [DataRow(new byte[] { 0x80 }, "\\x80", DisplayName = "0x80")]
    [DataRow(new byte[] { 0xFF }, "\\xFF", DisplayName = "0xFF")]
    [DataRow(new byte[] { (byte)'\r', (byte)'\n' }, "\\r\\n", DisplayName = "CR LF")]
    public void NamedCase_RendersExactText(byte[] bytes, string expected) =>
        Assert.AreEqual(expected, ExchangeLogEscaping.Escape(bytes));

    [TestMethod]
    public void RequestLine_RendersUnchanged() =>
        Assert.AreEqual("GET / HTTP/1.1", ExchangeLogEscaping.Escape("GET / HTTP/1.1"u8));

    [TestMethod]
    public void Rendering_RoundTripsEveryByte()
    {
        var everyByte = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();

        var rendering = ExchangeLogEscaping.Escape(everyByte);

        CollectionAssert.AreEqual(everyByte, Decode(rendering));
    }

    [TestMethod]
    public void Note_WithEscAndNonAsciiCharacter_IsWrittenEscaped()
    {
        using var writer = new StringWriter();
        var log = new LevelledExchangeLogFactory(writer, LogLevel.Verbose, stampTimes: false, TimeProvider.System).Create(7, Remote);

        log.Note("path \u001B[31m/caf\u00E9");

        Assert.AreEqual("#7 * path \\x1B[31m/caf\\xC3\\xA9" + Environment.NewLine, writer.ToString());
    }

    [TestMethod]
    public void BytesSent_AreRenderedByTheSameRuleAsBytesReceived()
    {
        var everyByteWithoutLineFeed = Enumerable.Range(0, 256)
            .Where(value => value != '\n')
            .Select(value => (byte)value)
            .ToArray();
        using var receivedWriter = new StringWriter();
        using var sentWriter = new StringWriter();

        new LevelledExchangeLogFactory(receivedWriter, LogLevel.Verbose, stampTimes: false, TimeProvider.System).Create(1, Remote).BytesReceived(everyByteWithoutLineFeed);
        new LevelledExchangeLogFactory(sentWriter, LogLevel.Verbose, stampTimes: false, TimeProvider.System).Create(1, Remote).BytesSent(everyByteWithoutLineFeed);

        var expected = ExchangeLogEscaping.Escape(everyByteWithoutLineFeed) + Environment.NewLine;
        Assert.AreEqual("#1 < " + expected, receivedWriter.ToString());
        Assert.AreEqual("#1 > " + expected, sentWriter.ToString());
    }

    private static string ExpectedByAdr0006(byte value) => value switch
    {
        0x0D => "\\r",
        0x0A => "\\n",
        >= 0x20 and <= 0x7E when value != 0x5C => ((char)value).ToString(),
        _ => "\\x" + value.ToString("X2", CultureInfo.InvariantCulture),
    };

    private static byte[] Decode(string rendering)
    {
        var bytes = new List<byte>();
        for (var index = 0; index < rendering.Length; index++)
        {
            if (rendering[index] != '\\')
            {
                bytes.Add((byte)rendering[index]);
                continue;
            }

            index++;
            switch (rendering[index])
            {
                case 'r':
                    bytes.Add(0x0D);
                    break;
                case 'n':
                    bytes.Add(0x0A);
                    break;
                case 'x':
                    bytes.Add(byte.Parse(rendering.AsSpan(index + 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
                    index += 2;
                    break;
                default:
                    Assert.Fail($"Unexpected escape \\{rendering[index]} at {index}.");
                    break;
            }
        }

        return [.. bytes];
    }
}
