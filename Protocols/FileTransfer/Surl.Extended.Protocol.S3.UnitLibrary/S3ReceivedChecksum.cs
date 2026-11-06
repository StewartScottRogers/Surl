namespace Surl.Extended.Protocol.S3;

/// <summary>
/// An additional checksum asked for and the one the body came to ("Checking object integrity";
/// ADR-0108 decision 4.5).
/// </summary>
/// <param name="Algorithm">The algorithm.</param>
/// <param name="Expected">The digest the field or the trailer carried.</param>
/// <param name="Computed">The digest of the object's bytes.</param>
internal sealed record S3ReceivedChecksum(S3ChecksumAlgorithm Algorithm, byte[] Expected, byte[] Computed)
{
    /// <summary>
    /// Whether the body is the one the checksum describes.
    /// </summary>
    public bool IsMatch => Expected.AsSpan().SequenceEqual(Computed);

    /// <summary>
    /// The field the answer echoes it in, such as <c>x-amz-checksum-crc32: &lt;base64&gt;</c>.
    /// </summary>
    public KeyValuePair<string, string> Field => new(Algorithm.FieldName, Convert.ToBase64String(Computed));
}
