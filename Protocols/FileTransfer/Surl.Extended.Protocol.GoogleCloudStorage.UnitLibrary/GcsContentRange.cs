using System.Globalization;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The <c>Content-Range</c> of a request sent to a resumable upload's session URI ("Resumable
/// uploads"; ADR-0110, decision 9): <c>bytes &lt;first&gt;-&lt;last&gt;/&lt;total&gt;</c> for a chunk,
/// <c>bytes */&lt;total&gt;</c> for a status check, with <c>*</c> for a total not yet known.
/// </summary>
/// <param name="First">The chunk's first byte, or <see langword="null"/> when the request carries none.</param>
/// <param name="Last">The chunk's last byte, or <see langword="null"/> when the request carries none.</param>
/// <param name="Total">The object's length, or <see langword="null"/> when not yet known.</param>
internal sealed record GcsContentRange(long? First, long? Last, long? Total)
{
    /// <summary>
    /// Whether the request carries bytes.
    /// </summary>
    public bool HasBytes => First is not null;

    /// <summary>
    /// How many bytes the chunk holds.
    /// </summary>
    public long Count => HasBytes ? Last!.Value - First!.Value + 1 : 0;

    /// <summary>
    /// The range a request means: its <c>Content-Range</c>, or, without one, the whole object in
    /// one chunk of the announced length.
    /// </summary>
    /// <param name="field">The <c>Content-Range</c> field's value, or <see langword="null"/>.</param>
    /// <param name="announcedLength">The body's announced length.</param>
    /// <returns>The range, or <see langword="null"/> when the field does not parse or a byte lies
    /// past the total.</returns>
    public static GcsContentRange? Of(string? field, long announcedLength)
    {
        if (field is null)
        {
            return announcedLength == 0 ? new(null, null, 0) : new(0, announcedLength - 1, announcedLength);
        }

        var value = field.Trim();
        var slash = value.IndexOf('/');
        if (!value.StartsWith("bytes ", StringComparison.OrdinalIgnoreCase) || slash < 0)
        {
            return null;
        }

        var bytes = value[6..slash].Trim();
        var total = value[(slash + 1)..].Trim();
        if (!TryTotal(total, out var length))
        {
            return null;
        }

        return bytes == "*" ? new(null, null, length) : Chunk(bytes, length);
    }

    private static GcsContentRange? Chunk(string bytes, long? total)
    {
        var dash = bytes.IndexOf('-');
        if (dash < 0 || !TryNumber(bytes[..dash], out var first) || !TryNumber(bytes[(dash + 1)..], out var last) || last < first || last >= total)
        {
            return null;
        }

        return new(first, last, total);
    }

    private static bool TryTotal(string text, out long? total)
    {
        total = null;
        if (text == "*")
        {
            return true;
        }

        var reads = TryNumber(text, out var number);
        total = number;

        return reads;
    }

    private static bool TryNumber(string text, out long number) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);
}
