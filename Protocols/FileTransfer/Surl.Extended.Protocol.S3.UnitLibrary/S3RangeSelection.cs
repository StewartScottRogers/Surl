using System.Globalization;
using Surl.Content;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Which bytes of an object GetObject and HeadObject answer for a <c>Range</c> field (GetObject;
/// ADR-0108, decision 1): one <c>bytes=</c> range is served <c>206</c>, an unsatisfiable one is
/// <c>416 InvalidRange</c>, and several ranges or a syntax the page does not give are ignored.
/// </summary>
internal static class S3RangeSelection
{
    private const string BytesUnit = "bytes=";

    /// <summary>
    /// Selects the bytes a <c>Range</c> field asks for.
    /// </summary>
    /// <param name="rangeField">The field's value, or <see langword="null"/> when none was sent.</param>
    /// <param name="objectLength">The object's length in bytes.</param>
    /// <returns>The range selected, unsatisfiable when it names no byte, or <see langword="null"/>
    /// when the whole object is served <c>200</c>.</returns>
    public static ContentByteRange? Select(string? rangeField, long objectLength)
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

        return first.Length == 0 ? SelectSuffix(last, objectLength) : SelectFrom(first, last, objectLength);
    }

    // bytes=-<n>: the last n bytes; a zero suffix names no byte.
    private static ContentByteRange? SelectSuffix(string suffixText, long objectLength)
    {
        if (!TryReadOffset(suffixText, out var suffixLength))
        {
            return null;
        }

        return suffixLength == 0 || objectLength == 0
            ? ContentByteRange.Select(objectLength, objectLength, objectLength)
            : ContentByteRange.Select(objectLength, Math.Max(0, objectLength - suffixLength), objectLength - 1);
    }

    // bytes=<first>- or bytes=<first>-<last>; a last before the first is no range the page gives.
    private static ContentByteRange? SelectFrom(string firstText, string lastText, long objectLength)
    {
        if (!TryReadOffset(firstText, out var first))
        {
            return null;
        }

        if (lastText.Length == 0)
        {
            return ContentByteRange.Select(objectLength, first, long.MaxValue);
        }

        return TryReadOffset(lastText, out var last) && last >= first
            ? ContentByteRange.Select(objectLength, first, last)
            : null;
    }

    private static bool TryReadOffset(string text, out long offset) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out offset);
}
