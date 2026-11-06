namespace Surl.Extended.Protocol.S3;

/// <summary>
/// One <c>Part</c> of a CompleteMultipartUpload body: the part number and the <c>ETag</c> the
/// client says the part has.
/// </summary>
/// <param name="PartNumber">The part's number, as sent.</param>
/// <param name="ETag">The part's ETag, as sent.</param>
internal sealed record S3CompletedPart(int PartNumber, string ETag);
