using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Unicode;
using Surl.Api;

namespace Surl.Mcp;

/// <summary>
/// Writes recorded exchanges as <c>get_exchanges</c> shows them (ADR-0103 decision 3): each chunk as <c>text</c>
/// when its bytes are well-formed UTF-8 holding no control character but tab, CR and LF, else as <c>base64</c>; at
/// most <see cref="MaxShownBytesPerExchange"/> bytes of each exchange and <see cref="MaxShownBytesPerResult"/> in
/// all, a cut ending a chunk early (backing off to the start of a UTF-8 sequence in a text chunk) and dropping the
/// chunks after it.
/// </summary>
internal static class RecordedExchangeWriter
{
    /// <summary>The most recorded bytes of one exchange shown.</summary>
    public const int MaxShownBytesPerExchange = 65536;

    /// <summary>The most recorded bytes shown in one result.</summary>
    public const int MaxShownBytesPerResult = 1_048_576;

    /// <summary>
    /// Writes the <c>exchanges</c> array: <paramref name="records"/> in order, at most <paramref name="maxExchanges"/>
    /// of them, leaving out every exchange after the result's byte cap is reached.
    /// </summary>
    /// <param name="writer">The writer, inside an object.</param>
    /// <param name="records">The exchanges to show, oldest first.</param>
    /// <param name="maxExchanges">The most exchanges to show.</param>
    /// <returns>Whether an exchange was left out, so the agent should ask again.</returns>
    public static bool WriteExchanges(Utf8JsonWriter writer, IEnumerable<SurlExchangeRecord> records, int maxExchanges)
    {
        var hasMore = false;
        var remainingBytes = MaxShownBytesPerResult;
        var shownExchangeCount = 0;
        writer.WriteStartArray("exchanges");
        foreach (var record in records)
        {
            if (shownExchangeCount == maxExchanges || remainingBytes == 0)
            {
                hasMore = true;
                break;
            }

            remainingBytes -= WriteExchange(writer, record, Math.Min(MaxShownBytesPerExchange, remainingBytes));
            shownExchangeCount++;
        }

        writer.WriteEndArray();
        return hasMore;
    }

    /// <summary>Whether <paramref name="bytes"/> are shown as text: well-formed UTF-8 with no control character but tab, CR and LF.</summary>
    /// <param name="bytes">A chunk's bytes.</param>
    /// <returns><see langword="true"/> for text, <see langword="false"/> for base64.</returns>
    public static bool IsText(ReadOnlySpan<byte> bytes) =>
        Utf8.IsValid(bytes) && !Encoding.UTF8.GetString(bytes).Any(IsControlButTabCrOrLf);

    private static bool IsControlButTabCrOrLf(char character) =>
        char.IsControl(character) && character is not ('\t' or '\r' or '\n');

    private static int WriteExchange(Utf8JsonWriter writer, SurlExchangeRecord record, int maxShownBytes)
    {
        writer.WriteStartObject();
        writer.WriteNumber("exchange_id", record.ExchangeId);
        writer.WriteString("listen_url", record.ListenUrl.ToString());
        writer.WriteString("scheme", record.Scheme);
        writer.WriteString("client", record.Client.ToString());
        writer.WriteString("started_at", record.StartedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        writer.WriteStartArray("chunks");
        var shownBytes = 0;
        var isCut = false;
        foreach (var chunk in record.Chunks)
        {
            var shownLength = Math.Min(chunk.Bytes.Length, maxShownBytes - shownBytes);
            isCut = shownLength < chunk.Bytes.Length;
            shownBytes += WriteChunk(writer, chunk, shownLength);
            if (isCut)
            {
                break;
            }
        }

        writer.WriteEndArray();
        writer.WriteNumber("recorded_bytes", record.Chunks.Sum(chunk => chunk.Bytes.Length));
        writer.WriteNumber("shown_bytes", shownBytes);
        writer.WriteBoolean("is_cut", isCut);
        writer.WriteBoolean("is_truncated", record.IsTruncated);
        writer.WriteEndObject();
        return shownBytes;
    }

    private static int WriteChunk(Utf8JsonWriter writer, SurlExchangeChunk chunk, int shownLength)
    {
        var bytes = chunk.Bytes.Span;
        var isText = IsText(bytes);
        if (isText)
        {
            shownLength = BackOffToSequenceStart(bytes, shownLength);
        }

        if (shownLength == 0)
        {
            return 0;
        }

        var shown = bytes[..shownLength];
        writer.WriteStartObject();
        writer.WriteString("direction", chunk.Direction == SurlExchangeDirection.Received ? "received" : "sent");
        writer.WriteString("encoding", isText ? "text" : "base64");
        writer.WriteString("data", isText ? Encoding.UTF8.GetString(shown) : Convert.ToBase64String(shown));
        writer.WriteEndObject();
        return shownLength;
    }

    private static int BackOffToSequenceStart(ReadOnlySpan<byte> bytes, int length)
    {
        while (length > 0 && length < bytes.Length && IsContinuationByte(bytes[length]))
        {
            length--;
        }

        return length;
    }

    private static bool IsContinuationByte(byte value) => (value & 0b1100_0000) == 0b1000_0000;
}
