using System.Net;
using System.Text;
using System.Text.Json;
using Surl.Api;

namespace Surl.Mcp;

[TestClass]
public sealed class RecordedExchangeWriterTests
{
    private static readonly IPEndPoint Client = new(IPAddress.Loopback, 50512);

    [TestMethod]
    public void WriteExchanges_NonUtf8Bytes_WritesTheChunkAsBase64AndMarksItSo()
    {
        byte[] bytes = [0x16, 0x03, 0x01, 0xFF, 0xFE];

        var (exchanges, _) = Write([Record(1, false, Received(bytes))]);

        var chunk = Assert.ContainsSingle(exchanges[0].GetProperty("chunks").EnumerateArray());
        Assert.AreEqual("received", chunk.GetProperty("direction").GetString());
        Assert.AreEqual("base64", chunk.GetProperty("encoding").GetString());
        Assert.AreEqual(Convert.ToBase64String(bytes), chunk.GetProperty("data").GetString());
    }

    [TestMethod]
    public void WriteExchanges_TextAndAControlCharacterChunk_WritesTheFirstAsTextAndTheSecondAsBase64InOrder()
    {
        var text = "220 ready\r\n\tok\n"u8.ToArray();
        var withEscape = "a\u001Bb"u8.ToArray();

        var (exchanges, _) = Write([Record(1, false, Sent(text), Received(withEscape))]);

        var chunks = exchanges[0].GetProperty("chunks").EnumerateArray().ToArray();
        Assert.AreEqual("sent", chunks[0].GetProperty("direction").GetString());
        Assert.AreEqual("text", chunks[0].GetProperty("encoding").GetString());
        Assert.AreEqual("220 ready\r\n\tok\n", chunks[0].GetProperty("data").GetString());
        Assert.AreEqual("received", chunks[1].GetProperty("direction").GetString());
        Assert.AreEqual("base64", chunks[1].GetProperty("encoding").GetString());
        CollectionAssert.AreEqual(withEscape, Convert.FromBase64String(chunks[1].GetProperty("data").GetString()!));
    }

    [TestMethod]
    [DataRow(new byte[] { 0x61, 0xC2, 0x85 }, DisplayName = "C1 control U+0085")]
    [DataRow(new byte[] { 0x61, 0x7F }, DisplayName = "DEL")]
    [DataRow(new byte[] { 0x61, 0x00 }, DisplayName = "NUL")]
    [DataRow(new byte[] { 0xC3, 0x28 }, DisplayName = "ill-formed UTF-8")]
    public void IsText_ControlCharacterOrIllFormedUtf8_IsFalse(byte[] bytes) =>
        Assert.IsFalse(RecordedExchangeWriter.IsText(bytes));

    [TestMethod]
    public void IsText_WellFormedUtf8WithTabCrAndLf_IsTrue() =>
        Assert.IsTrue(RecordedExchangeWriter.IsText("café\t\r\n€"u8));

    [TestMethod]
    public void WriteExchanges_ExchangePastThePerExchangeCap_CutsTheChunkDropsTheRestAndSaysSo()
    {
        var first = Bytes((byte)'a', RecordedExchangeWriter.MaxShownBytesPerExchange - 10);
        var second = Bytes((byte)'b', 20);

        var (exchanges, hasMore) = Write([Record(1, true, Received(first), Sent(second), Received(Bytes((byte)'c', 5)))]);

        var exchange = exchanges[0];
        var chunks = exchange.GetProperty("chunks").EnumerateArray().ToArray();
        Assert.HasCount(2, chunks);
        Assert.AreEqual(new string('b', 10), chunks[1].GetProperty("data").GetString());
        Assert.AreEqual(first.Length + 25, exchange.GetProperty("recorded_bytes").GetInt32());
        Assert.AreEqual(RecordedExchangeWriter.MaxShownBytesPerExchange, exchange.GetProperty("shown_bytes").GetInt32());
        Assert.IsTrue(exchange.GetProperty("is_cut").GetBoolean());
        Assert.IsTrue(exchange.GetProperty("is_truncated").GetBoolean());
        Assert.IsFalse(hasMore);
    }

    [TestMethod]
    public void WriteExchanges_CutInsideAUtf8Sequence_BacksOffToItsStartSoTheTextStaysWellFormed()
    {
        // 'é' is C3 A9: a cap landing between the two bytes shows the text before it.
        var text = Encoding.UTF8.GetBytes(new string('a', RecordedExchangeWriter.MaxShownBytesPerExchange - 1) + "é");

        var (exchanges, _) = Write([Record(1, false, Received(text))]);

        var chunk = Assert.ContainsSingle(exchanges[0].GetProperty("chunks").EnumerateArray());
        Assert.AreEqual("text", chunk.GetProperty("encoding").GetString());
        Assert.AreEqual(new string('a', RecordedExchangeWriter.MaxShownBytesPerExchange - 1), chunk.GetProperty("data").GetString());
        Assert.AreEqual(RecordedExchangeWriter.MaxShownBytesPerExchange - 1, exchanges[0].GetProperty("shown_bytes").GetInt32());
        Assert.IsTrue(exchanges[0].GetProperty("is_cut").GetBoolean());
    }

    [TestMethod]
    public void WriteExchanges_CutBacksOffToNothing_WritesNoChunkForIt()
    {
        var full = Bytes((byte)'a', RecordedExchangeWriter.MaxShownBytesPerExchange - 1);
        var accented = "é"u8.ToArray();

        var (exchanges, _) = Write([Record(1, false, Received(full), Sent(accented))]);

        Assert.ContainsSingle(exchanges[0].GetProperty("chunks").EnumerateArray());
        Assert.AreEqual(full.Length, exchanges[0].GetProperty("shown_bytes").GetInt32());
        Assert.IsTrue(exchanges[0].GetProperty("is_cut").GetBoolean());
    }

    [TestMethod]
    public void WriteExchanges_CutOfABinaryChunk_ShowsExactlyTheCap()
    {
        var binary = Bytes(0xFF, RecordedExchangeWriter.MaxShownBytesPerExchange + 3);

        var (exchanges, _) = Write([Record(1, false, Received(binary))]);

        var chunk = Assert.ContainsSingle(exchanges[0].GetProperty("chunks").EnumerateArray());
        Assert.HasCount(RecordedExchangeWriter.MaxShownBytesPerExchange, Convert.FromBase64String(chunk.GetProperty("data").GetString()!));
    }

    [TestMethod]
    public void WriteExchanges_ExchangesPastTheResultCap_LeavesTheRestOutAndSaysThereIsMore()
    {
        var perResult = RecordedExchangeWriter.MaxShownBytesPerResult / RecordedExchangeWriter.MaxShownBytesPerExchange;
        var records = Enumerable.Range(1, perResult + 1)
            .Select(id => Record(id, false, Received(Bytes((byte)'a', RecordedExchangeWriter.MaxShownBytesPerExchange))))
            .ToArray();

        var (exchanges, hasMore) = Write(records, maxExchanges: 100);

        Assert.HasCount(perResult, exchanges);
        Assert.IsTrue(hasMore);
    }

    [TestMethod]
    public void WriteExchanges_ExchangeCrossingTheResultCap_IsCutToWhatRemains()
    {
        var perResult = RecordedExchangeWriter.MaxShownBytesPerResult / RecordedExchangeWriter.MaxShownBytesPerExchange;
        var full = Bytes((byte)'a', RecordedExchangeWriter.MaxShownBytesPerExchange);
        var records = new[] { Record(1, false, Received(Bytes((byte)'a', 100))) }
            .Concat(Enumerable.Range(2, perResult).Select(id => Record(id, false, Received(full))))
            .ToArray();

        var (exchanges, hasMore) = Write(records, maxExchanges: 100);

        Assert.HasCount(perResult + 1, exchanges);
        var last = exchanges[^1];
        Assert.AreEqual(RecordedExchangeWriter.MaxShownBytesPerExchange - 100, last.GetProperty("shown_bytes").GetInt32());
        Assert.IsTrue(last.GetProperty("is_cut").GetBoolean());
        Assert.IsFalse(hasMore);
    }

    private static (JsonElement[] Exchanges, bool HasMore) Write(SurlExchangeRecord[] records, int maxExchanges = 20)
    {
        using var buffer = new MemoryStream();
        bool hasMore;
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            hasMore = RecordedExchangeWriter.WriteExchanges(writer, records, maxExchanges);
            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.ToArray());
        return ([.. document.RootElement.GetProperty("exchanges").EnumerateArray().Select(exchange => exchange.Clone())], hasMore);
    }

    private static SurlExchangeRecord Record(long exchangeId, bool isTruncated, params SurlExchangeChunk[] chunks) =>
        new(exchangeId, Client, new DateTimeOffset(2026, 11, 5, 11, 0, 0, TimeSpan.FromHours(1)), chunks, isTruncated);

    private static SurlExchangeChunk Received(byte[] bytes) => new(SurlExchangeDirection.Received, bytes);

    private static SurlExchangeChunk Sent(byte[] bytes) => new(SurlExchangeDirection.Sent, bytes);

    private static byte[] Bytes(byte value, int count) => Enumerable.Repeat(value, count).ToArray();
}
