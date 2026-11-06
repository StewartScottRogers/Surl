namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// One part of a multipart upload: where its bytes sit in the upload's staging session, their MD5,
/// whose quoted lower-case hexadecimal is the part's <c>ETag</c>, and when it was uploaded.
/// </summary>
/// <param name="Offset">Where the part's first byte is in the session.</param>
/// <param name="Size">How many bytes the part holds.</param>
/// <param name="Md5">The MD5 of the part's bytes.</param>
/// <param name="Uploaded">When the part was uploaded.</param>
internal sealed record GcsUploadedPart(long Offset, long Size, byte[] Md5, DateTimeOffset Uploaded)
{
    /// <summary>
    /// The part's <c>ETag</c>: its MD5, quoted lower-case hexadecimal.
    /// </summary>
    public string ETag => $"\"{Convert.ToHexStringLower(Md5)}\"";

    /// <summary>
    /// Whether <paramref name="eTag"/>, as a completion body names it, is this part's: the quotes
    /// are optional and the hexadecimal digits of either case.
    /// </summary>
    /// <param name="eTag">The ETag the client sent.</param>
    /// <returns>Whether it is the part's.</returns>
    public bool HasETag(string eTag) =>
        eTag.Trim().Trim('"').Equals(Convert.ToHexStringLower(Md5), StringComparison.OrdinalIgnoreCase);
}
