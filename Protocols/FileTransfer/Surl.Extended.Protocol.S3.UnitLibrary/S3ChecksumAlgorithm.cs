namespace Surl.Extended.Protocol.S3;

/// <summary>
/// An additional checksum algorithm ("Checking object integrity"; ADR-0108 decision 4.5): the
/// name its error message spells, the <c>x-amz-checksum-*</c> field that carries its base64
/// digest, and the digest's length.
/// </summary>
/// <param name="Name">The algorithm's name, upper case, such as <c>CRC32</c>.</param>
/// <param name="DigestLength">The digest's length in bytes.</param>
internal sealed record S3ChecksumAlgorithm(string Name, int DigestLength)
{
    /// <summary>
    /// CRC-32 (ISO-HDLC, the polynomial 0x04C11DB7 reflected).
    /// </summary>
    public static S3ChecksumAlgorithm Crc32 { get; } = new("CRC32", 4);

    /// <summary>
    /// CRC-32C (Castagnoli, the polynomial 0x1EDC6F41 reflected).
    /// </summary>
    public static S3ChecksumAlgorithm Crc32C { get; } = new("CRC32C", 4);

    /// <summary>
    /// SHA-1.
    /// </summary>
    public static S3ChecksumAlgorithm Sha1 { get; } = new("SHA1", 20);

    /// <summary>
    /// SHA-256.
    /// </summary>
    public static S3ChecksumAlgorithm Sha256 { get; } = new("SHA256", 32);

    /// <summary>
    /// The field of the one algorithm the server does not serve, CRC-64/NVME: <c>501 NotImplemented</c>.
    /// </summary>
    public const string Crc64NvmeFieldName = "x-amz-checksum-crc64nvme";

    /// <summary>
    /// The algorithms the server checks.
    /// </summary>
    public static IReadOnlyList<S3ChecksumAlgorithm> Served { get; } = [Crc32, Crc32C, Sha1, Sha256];

    /// <summary>
    /// The field carrying the digest, such as <c>x-amz-checksum-crc32</c>.
    /// </summary>
    public string FieldName => "x-amz-checksum-" + Name.ToLowerInvariant();

    /// <summary>
    /// The served algorithm whose field is <paramref name="fieldName"/>, compared without regard to case.
    /// </summary>
    /// <param name="fieldName">A field name.</param>
    /// <returns>The algorithm, or <see langword="null"/>.</returns>
    public static S3ChecksumAlgorithm? OfFieldName(string fieldName) =>
        Served.FirstOrDefault(algorithm => algorithm.FieldName.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
}
