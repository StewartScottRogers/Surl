using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Surl.Mcp;

/// <summary>
/// How the read-back tools write what a store held (ADR-0164 decision 6): bytes as <c>text</c> when they are
/// well-formed UTF-8 with no control character other than tab, CR and LF, otherwise as <c>base64</c>; a result holds
/// one of the two. Times are ISO 8601 in UTC, as the other tools write them.
/// </summary>
internal static class ReadBackJson
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Writes <paramref name="bytes"/> as <paramref name="textName"/> or, when not plain text, as <paramref name="base64Name"/>.</summary>
    /// <param name="writer">The writer, inside an object.</param>
    /// <param name="bytes">The bytes.</param>
    /// <param name="textName">The member's name when the bytes are text.</param>
    /// <param name="base64Name">The member's name when the bytes are not text.</param>
    public static void WriteBytes(Utf8JsonWriter writer, ReadOnlyMemory<byte> bytes, string textName = "text", string base64Name = "base64")
    {
        if (TryGetText(bytes.Span) is { } text)
        {
            writer.WriteString(textName, text);
        }
        else
        {
            writer.WriteBase64String(base64Name, bytes.Span);
        }
    }

    /// <summary>Writes <paramref name="time"/> as <paramref name="name"/>, in UTC.</summary>
    /// <param name="writer">The writer, inside an object.</param>
    /// <param name="name">The member's name.</param>
    /// <param name="time">The time.</param>
    public static void WriteTime(Utf8JsonWriter writer, string name, DateTimeOffset time) =>
        writer.WriteString(name, time.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    private static string? TryGetText(ReadOnlySpan<byte> bytes)
    {
        string text;
        try
        {
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }

        foreach (var character in text)
        {
            if (char.IsControl(character) && character is not ('\t' or '\r' or '\n'))
            {
                return null;
            }
        }

        return text;
    }
}
