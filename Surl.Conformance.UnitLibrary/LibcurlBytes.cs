using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// How the libcurl drivers read bytes from their command line and write them to standard output:
/// backslash escapes in, printable ASCII with <c>\xHH</c> for everything else out.
/// </summary>
public static class LibcurlBytes
{
    /// <summary>
    /// The longest run of bytes <see cref="Describe"/> shows byte for byte; a longer one is shown
    /// as its SHA-256.
    /// </summary>
    public const int LongestShown = 256;

    /// <summary>
    /// Turns command-line text into bytes, one per character, reading the escapes <c>\r</c>,
    /// <c>\n</c>, <c>\t</c>, <c>\0</c>, <c>\\</c> and <c>\xHH</c>; a backslash before any other
    /// character, or last, stands for that character.
    /// </summary>
    /// <param name="text">The text, every character of it at most U+00FF.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="FormatException">A character is above U+00FF, or <c>\x</c> is not followed by two hexadecimal digits.</exception>
    public static byte[] Unescape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var bytes = new List<byte>(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\\' || index + 1 == text.Length)
            {
                bytes.Add(Latin1(text[index]));
                continue;
            }

            var escape = text[++index];
            if (escape == 'x')
            {
                bytes.Add(Hexadecimal(text, index + 1));
                index += 2;
                continue;
            }

            bytes.Add(escape switch { 'r' => 13, 'n' => 10, 't' => 9, '0' => 0, _ => Latin1(escape) });
        }

        return [.. bytes];
    }

    /// <summary>
    /// Writes bytes as text: printable ASCII as itself, a backslash as <c>\\</c>, a double quote as
    /// <c>\"</c> and every other byte as <c>\xHH</c>.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The text.</returns>
    public static string Show(ReadOnlySpan<byte> bytes)
    {
        var text = new StringBuilder(bytes.Length);
        foreach (var value in bytes)
        {
            text.Append(value switch
            {
                (byte)'\\' => "\\\\",
                (byte)'"' => "\\\"",
                >= 0x20 and < 0x7F => ((char)value).ToString(),
                _ => $"\\x{value:X2}",
            });
        }

        return text.ToString();
    }

    /// <summary>
    /// Describes a run of bytes for a line of a driver's output: <c>5 bytes "hello"</c>, or, past
    /// <see cref="LongestShown"/> bytes, <c>300 bytes sha256 &lt;hex&gt;</c>.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The description.</returns>
    public static string Describe(ReadOnlySpan<byte> bytes) =>
        bytes.Length <= LongestShown
            ? $"{bytes.Length} bytes \"{Show(bytes)}\""
            : $"{bytes.Length} bytes sha256 {Convert.ToHexString(SHA256.HashData(bytes))}";

    private static byte Latin1(char character) =>
        character <= 'ÿ' ? (byte)character : throw new FormatException($"'{character}' is not a single byte; write it as \\xHH.");

    private static byte Hexadecimal(string text, int start) =>
        start + 2 <= text.Length && byte.TryParse(text.AsSpan(start, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException("\\x needs two hexadecimal digits.");
}
