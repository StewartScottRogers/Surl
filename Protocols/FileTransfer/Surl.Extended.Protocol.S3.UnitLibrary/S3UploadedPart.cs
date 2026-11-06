namespace Surl.Extended.Protocol.S3;

/// <summary>
/// One part of a multipart upload: where its bytes sit in the upload's session, and their MD5,
/// whose quoted lower-case hexadecimal is the part's <c>ETag</c>.
/// </summary>
/// <param name="Offset">Where the part's first byte is in the session.</param>
/// <param name="Size">How many bytes the part holds.</param>
/// <param name="Md5">The MD5 of the part's bytes.</param>
internal sealed record S3UploadedPart(long Offset, long Size, byte[] Md5)
{
    /// <summary>
    /// The part's <c>ETag</c>: its MD5, quoted lower-case hexadecimal.
    /// </summary>
    public string ETag => S3PutObject.ETagOf(Md5);

    /// <summary>
    /// Whether <paramref name="eTag"/>, as a CompleteMultipartUpload body names it, is this part's:
    /// the quotes are optional and the hexadecimal digits of either case.
    /// </summary>
    /// <param name="eTag">The ETag the client sent.</param>
    /// <returns>Whether it is the part's.</returns>
    public bool HasETag(string eTag) =>
        eTag.Trim().Trim('"').Equals(Convert.ToHexStringLower(Md5), StringComparison.OrdinalIgnoreCase);
}
