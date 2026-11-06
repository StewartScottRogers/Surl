namespace Surl.Hpack;

/// <summary>
/// The static table of RFC 7541 Appendix A: 61 fixed entries, indexes 1 to 61.
/// </summary>
internal static class HpackStaticTable
{
    /// <summary>The number of entries; dynamic table indexes start after it.</summary>
    public const int Count = 61;

    private static readonly HpackHeaderField[] Entries =
    [
        new(":authority", string.Empty),
        new(":method", "GET"),
        new(":method", "POST"),
        new(":path", "/"),
        new(":path", "/index.html"),
        new(":scheme", "http"),
        new(":scheme", "https"),
        new(":status", "200"),
        new(":status", "204"),
        new(":status", "206"),
        new(":status", "304"),
        new(":status", "400"),
        new(":status", "404"),
        new(":status", "500"),
        new("accept-charset", string.Empty),
        new("accept-encoding", "gzip, deflate"),
        new("accept-language", string.Empty),
        new("accept-ranges", string.Empty),
        new("accept", string.Empty),
        new("access-control-allow-origin", string.Empty),
        new("age", string.Empty),
        new("allow", string.Empty),
        new("authorization", string.Empty),
        new("cache-control", string.Empty),
        new("content-disposition", string.Empty),
        new("content-encoding", string.Empty),
        new("content-language", string.Empty),
        new("content-length", string.Empty),
        new("content-location", string.Empty),
        new("content-range", string.Empty),
        new("content-type", string.Empty),
        new("cookie", string.Empty),
        new("date", string.Empty),
        new("etag", string.Empty),
        new("expect", string.Empty),
        new("expires", string.Empty),
        new("from", string.Empty),
        new("host", string.Empty),
        new("if-match", string.Empty),
        new("if-modified-since", string.Empty),
        new("if-none-match", string.Empty),
        new("if-range", string.Empty),
        new("if-unmodified-since", string.Empty),
        new("last-modified", string.Empty),
        new("link", string.Empty),
        new("location", string.Empty),
        new("max-forwards", string.Empty),
        new("proxy-authenticate", string.Empty),
        new("proxy-authorization", string.Empty),
        new("range", string.Empty),
        new("referer", string.Empty),
        new("refresh", string.Empty),
        new("retry-after", string.Empty),
        new("server", string.Empty),
        new("set-cookie", string.Empty),
        new("strict-transport-security", string.Empty),
        new("transfer-encoding", string.Empty),
        new("user-agent", string.Empty),
        new("vary", string.Empty),
        new("via", string.Empty),
        new("www-authenticate", string.Empty),
    ];

    /// <summary>
    /// Gets the entry at a 1-based index.
    /// </summary>
    /// <param name="index">1 to 61.</param>
    /// <returns>The entry.</returns>
    public static HpackHeaderField Get(int index) => Entries[index - 1];

    /// <summary>
    /// Finds a field in the static table: the entry whose name
    /// and value both match if there is one, otherwise the first entry with the name.
    /// </summary>
    /// <param name="name">The field name.</param>
    /// <param name="value">The field value.</param>
    /// <param name="nameOnly">Match names only, never name and value.</param>
    /// <returns>The 1-based index and whether the value matched too, or index 0 when no entry has the name.</returns>
    public static (int Index, bool IsExact) Find(string name, string value, bool nameOnly)
    {
        var firstWithName = 0;
        for (var index = 1; index <= Count; index++)
        {
            var entry = Entries[index - 1];
            if (entry.Name != name)
            {
                continue;
            }

            if (!nameOnly && entry.Value == value)
            {
                return (index, true);
            }

            firstWithName = firstWithName == 0 ? index : firstWithName;
        }

        return (firstWithName, false);
    }
}
