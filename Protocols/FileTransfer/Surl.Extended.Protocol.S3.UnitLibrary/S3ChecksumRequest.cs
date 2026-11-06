namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The additional checksum a write asks the server to check ("Checking object integrity";
/// ADR-0108 decision 4.5): one <c>x-amz-checksum-*</c> field, or the one an <c>aws-chunked</c>
/// body's trailer will carry, named by <c>x-amz-trailer</c>.
/// </summary>
/// <param name="Algorithm">The algorithm.</param>
/// <param name="SentDigest">The digest the field carries, or <see langword="null"/> when it comes in the trailer.</param>
internal sealed record S3ChecksumRequest(S3ChecksumAlgorithm Algorithm, byte[]? SentDigest)
{
    private const string TrailerFieldName = "x-amz-trailer";

    private static readonly string[] AlgorithmFieldNames =
        [.. S3ChecksumAlgorithm.Served.Select(algorithm => algorithm.FieldName), S3ChecksumAlgorithm.Crc64NvmeFieldName];

    /// <summary>
    /// Reads the checksum a write's fields ask for: <see langword="null"/> when they ask for none.
    /// CRC-64/NVME is <c>501 NotImplemented</c>; two algorithms named <c>400 InvalidRequest</c>
    /// (<see cref="S3Error.MultipleChecksums"/>); a value that is not the base64 of the
    /// algorithm's digest, or a trailer named on a body with no trailer or naming no checksum,
    /// <see cref="S3Error.InvalidChecksum"/>.
    /// </summary>
    /// <param name="fields">The request's fields.</param>
    /// <param name="form">How the body is framed.</param>
    /// <param name="refusal">The error refusing the request, or <see langword="null"/>.</param>
    /// <returns>The checksum asked for, or <see langword="null"/>.</returns>
    public static S3ChecksumRequest? Read(IReadOnlyList<KeyValuePair<string, string>> fields, S3PayloadForm form, out S3Error? refusal)
    {
        var trailer = S3RequestFields.Value(fields, TrailerFieldName)?.Trim();
        string[] named = [.. AlgorithmFieldNames.Where(name => S3RequestFields.Value(fields, name) is not null), .. trailer is null ? [] : new[] { trailer }];
        refusal = NamingRefusal(named, trailer, form);
        if (refusal is not null || named.Length == 0)
        {
            return null;
        }

        var algorithm = S3ChecksumAlgorithm.OfFieldName(named[0])!;
        if (trailer is not null)
        {
            return new S3ChecksumRequest(algorithm, null);
        }

        var digest = ReadDigest(S3RequestFields.Value(fields, named[0])!, algorithm);
        refusal = digest is null ? S3Error.InvalidChecksum : null;

        return digest is null ? null : new S3ChecksumRequest(algorithm, digest);
    }

    /// <summary>
    /// The digest a base64 value carries when it is <paramref name="algorithm"/>'s length.
    /// </summary>
    /// <param name="value">The value as sent.</param>
    /// <param name="algorithm">The algorithm.</param>
    /// <returns>The digest, or <see langword="null"/>.</returns>
    public static byte[]? ReadDigest(string value, S3ChecksumAlgorithm algorithm)
    {
        var bytes = new byte[64];

        return Convert.TryFromBase64String(value.Trim(), bytes, out var written) && written == algorithm.DigestLength ? bytes[..written] : null;
    }

    // CRC-64/NVME first, then more than one, then a trailer the body cannot carry or naming no
    // served algorithm.
    private static S3Error? NamingRefusal(string[] named, string? trailer, S3PayloadForm form)
    {
        if (named.Any(name => name.Equals(S3ChecksumAlgorithm.Crc64NvmeFieldName, StringComparison.OrdinalIgnoreCase)))
        {
            return S3Error.NotImplemented;
        }

        if (named.Length > 1)
        {
            return S3Error.MultipleChecksums;
        }

        return trailer is not null && (form != S3PayloadForm.UnsignedChunksWithTrailer || S3ChecksumAlgorithm.OfFieldName(trailer) is null)
            ? S3Error.InvalidChecksum
            : null;
    }
}
