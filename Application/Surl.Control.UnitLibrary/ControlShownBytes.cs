using System.Text.Unicode;

namespace Surl.Control;

/// <summary>
/// Bytes as the control endpoint shows them (ADR-0103 decision 3's rule): <c>text</c> when they are well-formed
/// UTF-8 holding no control character but tab, CR and LF, else <c>base64</c>; a cut backs off to the start of a
/// UTF-8 sequence so text stays well-formed.
/// </summary>
/// <param name="Encoding"><c>text</c> or <c>base64</c>.</param>
/// <param name="Data">The shown bytes as that encoding writes them.</param>
/// <param name="ShownLength">How many bytes are shown.</param>
internal sealed record ControlShownBytes(string Encoding, string Data, int ShownLength)
{
    /// <summary>Shows at most <paramref name="maxLength"/> of <paramref name="bytes"/>.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="maxLength">The most bytes shown.</param>
    /// <returns>What is shown.</returns>
    public static ControlShownBytes Show(ReadOnlySpan<byte> bytes, int maxLength)
    {
        var shownLength = Math.Min(bytes.Length, maxLength);
        if (!IsText(bytes))
        {
            return new ControlShownBytes("base64", Convert.ToBase64String(bytes[..shownLength]), shownLength);
        }

        while (shownLength > 0 && shownLength < bytes.Length && IsContinuationByte(bytes[shownLength]))
        {
            shownLength--;
        }

        return new ControlShownBytes("text", System.Text.Encoding.UTF8.GetString(bytes[..shownLength]), shownLength);
    }

    private static bool IsText(ReadOnlySpan<byte> bytes) =>
        Utf8.IsValid(bytes) && !System.Text.Encoding.UTF8.GetString(bytes).Any(IsControlButTabCrOrLf);

    private static bool IsControlButTabCrOrLf(char character) =>
        char.IsControl(character) && character is not ('\t' or '\r' or '\n');

    private static bool IsContinuationByte(byte value) => (value & 0b1100_0000) == 0b1000_0000;
}
