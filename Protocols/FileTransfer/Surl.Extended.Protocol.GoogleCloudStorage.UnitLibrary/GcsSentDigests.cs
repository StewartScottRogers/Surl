namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The digests a PUT Object says its body has (ADR-0110, decision 1.2): <c>Content-MD5</c>, and
/// each <c>x-goog-hash</c> field's <c>crc32c=</c> and <c>md5=</c> values, any number of them in one
/// field separated by commas or in fields of their own.
/// </summary>
/// <param name="ContentMd5">The MD5 <c>Content-MD5</c> gives, or <see langword="null"/>.</param>
/// <param name="HashCrc32c">The CRC-32C <c>x-goog-hash</c> gives, or <see langword="null"/>.</param>
/// <param name="HashMd5">The MD5 <c>x-goog-hash</c> gives, or <see langword="null"/>.</param>
internal sealed record GcsSentDigests(byte[]? ContentMd5, byte[]? HashCrc32c, byte[]? HashMd5)
{
    /// <summary>
    /// Reads the digests a request's fields give.
    /// </summary>
    /// <param name="fields">The request's fields.</param>
    /// <param name="refusal"><c>InvalidDigest</c> for a malformed <c>Content-MD5</c>, <c>InvalidHash</c>
    /// for a malformed <c>x-goog-hash</c>, else <see langword="null"/>.</param>
    /// <returns>The digests, or <see langword="null"/> when one is malformed.</returns>
    public static GcsSentDigests? Read(IReadOnlyList<KeyValuePair<string, string>> fields, out GcsError? refusal)
    {
        byte[]? contentMd5 = null;
        if (GcsRequestFields.Value(fields, "Content-MD5") is { } contentMd5Text && (contentMd5 = Decode(contentMd5Text, 16)) is null)
        {
            refusal = GcsError.InvalidDigest;
            return null;
        }

        byte[]? crc32c = null;
        byte[]? md5 = null;
        foreach (var item in HashItems(fields))
        {
            var valid = item.StartsWith("crc32c=", StringComparison.Ordinal) ? (crc32c = Decode(item[7..], 4)) is not null
                : item.StartsWith("md5=", StringComparison.Ordinal) && (md5 = Decode(item[4..], 16)) is not null;
            if (!valid)
            {
                refusal = GcsError.InvalidHash;
                return null;
            }
        }

        refusal = null;
        return new GcsSentDigests(contentMd5, crc32c, md5);
    }

    /// <summary>
    /// Whether the body's digests match every digest sent.
    /// </summary>
    /// <param name="body">The body's digests.</param>
    /// <returns><see langword="true"/> when nothing sent differs from the body.</returns>
    public bool Match(GcsObjectDigests body) =>
        Matches(ContentMd5, body.Md5) && Matches(HashMd5, body.Md5) && Matches(HashCrc32c, body.Crc32c);

    private static bool Matches(byte[]? sent, byte[] received) =>
        sent is null || sent.AsSpan().SequenceEqual(received);

    private static IEnumerable<string> HashItems(IReadOnlyList<KeyValuePair<string, string>> fields) =>
        fields.Where(field => field.Key.Equals("x-goog-hash", StringComparison.OrdinalIgnoreCase))
            .SelectMany(field => field.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

    // The bytes a base64 value holds when it holds exactly byteCount of them, else null.
    private static byte[]? Decode(string base64, int byteCount)
    {
        var bytes = new byte[64];

        return Convert.TryFromBase64String(base64.Trim(), bytes, out var written) && written == byteCount ? bytes[..byteCount] : null;
    }
}
