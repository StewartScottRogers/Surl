using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Surl.Mcp;

/// <summary>
/// Reads the content a seeding tool is given (ADR-0124 decision 8): exactly one of a UTF-8 text property and a
/// base64 property, as <c>start_server</c>'s <c>files</c> and <c>files_base64</c> carry content, at most
/// <see cref="MaxBytes"/> once decoded. No local path is ever read.
/// </summary>
internal static class SeededContent
{
    /// <summary>The most bytes one seed carries once decoded: 16 MiB.</summary>
    public const int MaxBytes = 16 * 1024 * 1024;

    /// <summary>Reads the content from <paramref name="textName"/> or <paramref name="base64Name"/>.</summary>
    /// <param name="arguments">The call's arguments; the schema has checked each is a string when given.</param>
    /// <param name="textName">The property holding UTF-8 text.</param>
    /// <param name="base64Name">The property holding base64.</param>
    /// <param name="content">The decoded bytes, or <see langword="null"/> when refused.</param>
    /// <returns>Why the content is refused, or <see langword="null"/>.</returns>
    public static string? TryRead(JsonElement arguments, string textName, string base64Name, out byte[]? content)
    {
        content = null;
        var text = RunningServerTool.ReadOptionalString(arguments, textName);
        var base64 = RunningServerTool.ReadOptionalString(arguments, base64Name);
        if ((text is null) == (base64 is null))
        {
            return $"give exactly one of {textName} and {base64Name}";
        }

        var (name, bytes) = text is not null ? (textName, Encoding.UTF8.GetBytes(text)) : (base64Name, DecodeBase64(base64!));
        if (bytes is null)
        {
            return $"{base64Name}: expected a base64 string";
        }

        if (bytes.Length > MaxBytes)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{name}: {bytes.Length} bytes is more than the {MaxBytes} (16 MiB) one seed may carry");
        }

        content = bytes;
        return null;
    }

    private static byte[]? DecodeBase64(string text)
    {
        var bytes = new byte[text.Length];
        return Convert.TryFromBase64String(text, bytes, out var written) ? bytes[..written] : null;
    }
}
