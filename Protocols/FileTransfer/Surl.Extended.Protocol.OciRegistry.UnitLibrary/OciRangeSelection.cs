using System.Globalization;
using Surl.Content;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Which bytes of a blob a <c>Range</c> field asks for (ADR-0115, decision 5.2; RFC 9110
/// section 14): one <c>bytes=</c> range is served <c>206</c>, an unsatisfiable one <c>416</c>,
/// and several ranges or another syntax are ignored, so the whole blob is served <c>200</c>.
/// </summary>
internal static class OciRangeSelection
{
    private const string BytesUnit = "bytes=";

    /// <summary>
    /// Selects the bytes a <c>Range</c> field asks for.
    /// </summary>
    /// <param name="rangeField">The field's value, or <see langword="null"/> when none was sent.</param>
    /// <param name="length">The blob's length in bytes.</param>
    /// <returns>The range selected, unsatisfiable when it names no byte, or <see langword="null"/>
    /// when the whole blob is served.</returns>
    public static ContentByteRange? Select(string? rangeField, long length)
    {
        if (rangeField is null || !rangeField.StartsWith(BytesUnit, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var spec = rangeField[BytesUnit.Length..].Trim();
        var dash = spec.IndexOf('-');
        if (spec.Contains(',') || dash < 0)
        {
            return null;
        }

        var first = spec[..dash];
        var last = spec[(dash + 1)..];

        return first.Length == 0 ? SelectSuffix(last, length) : SelectFrom(first, last, length);
    }

    // bytes=-<n>: the last n bytes; a zero suffix, or any suffix of an empty blob, names no byte.
    private static ContentByteRange? SelectSuffix(string suffixText, long length)
    {
        if (!TryReadOffset(suffixText, out var suffixLength))
        {
            return null;
        }

        return suffixLength == 0 || length == 0
            ? ContentByteRange.Select(length, length, length)
            : ContentByteRange.Select(length, Math.Max(0, length - suffixLength), length - 1);
    }

    // bytes=<first>- or bytes=<first>-<last>; a last before the first is no valid range.
    private static ContentByteRange? SelectFrom(string firstText, string lastText, long length)
    {
        if (!TryReadOffset(firstText, out var first))
        {
            return null;
        }

        if (lastText.Length == 0)
        {
            return ContentByteRange.Select(length, first, long.MaxValue);
        }

        return TryReadOffset(lastText, out var last) && last >= first
            ? ContentByteRange.Select(length, first, last)
            : null;
    }

    private static bool TryReadOffset(string text, out long offset) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out offset);
}
