using System.Globalization;
using Surl.Content;

namespace Surl.Protocol.Http;

/// <summary>
/// Which bytes of a file a <c>GET</c>'s <c>Range</c> field asks for (RFC 9110, section 14;
/// ADR-0133, decision 1): one <c>bytes</c> range is selected, a range that names no byte the
/// file has is unsatisfiable, and anything else - no field, another unit, a syntax RFC 9110
/// does not give, or several ranges - is ignored, so the whole file is served.
/// </summary>
internal static class HttpRangeSelection
{
    private const string BytesUnit = "bytes=";

    /// <summary>
    /// Selects the bytes a <c>Range</c> field asks for.
    /// </summary>
    /// <param name="rangeField">The field's value, or <see langword="null"/> when none was sent.</param>
    /// <param name="fileLength">The file's length in bytes.</param>
    /// <returns>The range selected, unsatisfiable when it names no byte, or
    /// <see langword="null"/> when the whole file is served.</returns>
    public static ContentByteRange? Select(string? rangeField, long fileLength)
    {
        if (rangeField is null || !rangeField.StartsWith(BytesUnit, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var spec = rangeField[BytesUnit.Length..].Trim(' ', '\t');
        var dash = spec.IndexOf('-', StringComparison.Ordinal);
        if (spec.Contains(',', StringComparison.Ordinal) || dash < 0)
        {
            return null;
        }

        var first = spec[..dash];
        var last = spec[(dash + 1)..];

        return first.Length == 0 ? SelectSuffix(last, fileLength) : SelectFrom(first, last, fileLength);
    }

    // bytes=-<n>: the last n bytes; a zero suffix, or any suffix of an empty file, names no byte.
    private static ContentByteRange? SelectSuffix(string suffixText, long fileLength)
    {
        if (!TryReadOffset(suffixText, out var suffixLength))
        {
            return null;
        }

        return suffixLength == 0 || fileLength == 0
            ? ContentByteRange.Select(fileLength, fileLength, fileLength)
            : ContentByteRange.Select(fileLength, Math.Max(0, fileLength - suffixLength), fileLength - 1);
    }

    // bytes=<first>- or bytes=<first>-<last>; a last before the first is an invalid range.
    private static ContentByteRange? SelectFrom(string firstText, string lastText, long fileLength)
    {
        if (!TryReadOffset(firstText, out var first))
        {
            return null;
        }

        if (lastText.Length == 0)
        {
            return ContentByteRange.Select(fileLength, first, long.MaxValue);
        }

        return TryReadOffset(lastText, out var last) && last >= first
            ? ContentByteRange.Select(fileLength, first, last)
            : null;
    }

    private static bool TryReadOffset(string text, out long offset) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out offset);
}
