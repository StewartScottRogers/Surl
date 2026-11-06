using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Surl.McpServing;

/// <summary>
/// The opaque cursor of a paged list (ADR-0103 decision 1): the next page's offset as invariant decimal text,
/// base64url encoded without padding.
/// </summary>
internal static class McpListCursor
{
    // An int offset is at most 10 digits, which encode to 14 characters; anything longer is refused undecoded.
    private const int MaximumCursorLength = 14;

    /// <summary>Encodes the offset of the next page.</summary>
    /// <param name="offset">The next page's offset.</param>
    /// <returns>The cursor.</returns>
    internal static string Encode(int offset) =>
        Base64Url.EncodeToString(Encoding.ASCII.GetBytes(offset.ToString(CultureInfo.InvariantCulture)));

    /// <summary>
    /// Reads where a list request starts: 0 when the request has no <c>params</c> or no <c>cursor</c>, otherwise
    /// the cursor's offset, which must be greater than 0 and less than <paramref name="count" />.
    /// </summary>
    /// <param name="parameters">The request's <c>params</c>.</param>
    /// <param name="count">How many entries the list holds.</param>
    /// <param name="offset">The first entry of the page.</param>
    /// <returns><see langword="false" /> when the <c>params</c> or the cursor is invalid.</returns>
    internal static bool TryReadOffset(JsonElement? parameters, int count, out int offset)
    {
        offset = 0;
        if (parameters is not { } value)
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!value.TryGetProperty("cursor", out JsonElement cursor))
        {
            return true;
        }

        return cursor.ValueKind == JsonValueKind.String && TryDecode(cursor.GetString()!, count, out offset);
    }

    private static bool TryDecode(string cursor, int count, out int offset)
    {
        offset = 0;
        if (cursor.Length > MaximumCursorLength)
        {
            return false;
        }

        byte[] text = new byte[Base64Url.GetMaxDecodedLength(cursor.Length)];
        if (Base64Url.DecodeFromChars(cursor, text, out _, out int written) != OperationStatus.Done)
        {
            return false;
        }

        return int.TryParse(text.AsSpan(0, written), NumberStyles.None, CultureInfo.InvariantCulture, out offset)
            && offset > 0
            && offset < count;
    }
}
