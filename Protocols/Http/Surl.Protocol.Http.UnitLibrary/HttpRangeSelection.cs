using System.Globalization;
using Surl.Content;

namespace Surl.Protocol.Http;

/// <summary>
/// Which bytes of a file a <c>GET</c>'s <c>Range</c> field asks for (RFC 9110, section 14;
/// ADR-0133, decision 1 and its Amendment 1): each <c>bytes</c> range is read, ranges naming no
/// byte the file has are dropped, and ranges that overlap or touch are coalesced. No field, another
/// unit, a syntax RFC 9110 does not give in any range, or more than <see cref="MaxRanges"/> ranges
/// is ignored, so the whole file is served.
/// </summary>
internal static class HttpRangeSelection
{
    /// <summary>
    /// The most ranges one field may name before the server ignores it and serves the whole file
    /// (ADR-0133, Amendment 1).
    /// </summary>
    public const int MaxRanges = 100;

    private const string BytesUnit = "bytes=";

    /// <summary>
    /// Selects the bytes a <c>Range</c> field asks for.
    /// </summary>
    /// <param name="rangeField">The field's value, or <see langword="null"/> when none was sent.</param>
    /// <param name="fileLength">The file's length in bytes.</param>
    /// <returns>The satisfiable ranges, coalesced, in the order sent unless coalescing sorted them;
    /// empty when no range names a byte (a <c>416</c>); or <see langword="null"/> when the whole
    /// file is served.</returns>
    public static IReadOnlyList<ContentByteRange>? Select(string? rangeField, long fileLength)
    {
        if (rangeField is null || !rangeField.StartsWith(BytesUnit, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // RFC 9110 section 5.6.1: a list's empty elements are skipped.
        var specs = rangeField[BytesUnit.Length..].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (specs.Length is 0 or > MaxRanges)
        {
            return null;
        }

        var ranges = new List<ContentByteRange>(specs.Length);
        foreach (var spec in specs)
        {
            var range = SelectOne(spec, fileLength);
            if (range is null)
            {
                return null;
            }

            if (range.IsSatisfiable)
            {
                ranges.Add(range);
            }
        }

        return Coalesce(ranges, fileLength);
    }

    private static ContentByteRange? SelectOne(string spec, long fileLength)
    {
        var dash = spec.IndexOf('-', StringComparison.Ordinal);
        if (dash < 0)
        {
            return null;
        }

        var first = spec[..dash];
        var last = spec[(dash + 1)..];

        return first.Length == 0 ? SelectSuffix(last, fileLength) : SelectFrom(first, last, fileLength);
    }

    // Ranges that overlap or touch become one, sorted (RFC 9110 section 14.2 lets a server
    // coalesce); when none do, the ranges keep the order they were sent in (section 14.6).
    private static List<ContentByteRange> Coalesce(List<ContentByteRange> ranges, long fileLength)
    {
        var merged = new List<ContentByteRange>(ranges.Count);
        foreach (var range in ranges.OrderBy(range => range.FirstByte))
        {
            if (merged.Count > 0 && range.FirstByte <= merged[^1].LastByte + 1)
            {
                merged[^1] = ContentByteRange.Select(fileLength, merged[^1].FirstByte, Math.Max(merged[^1].LastByte, range.LastByte));
            }
            else
            {
                merged.Add(range);
            }
        }

        return merged.Count == ranges.Count ? ranges : merged;
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
