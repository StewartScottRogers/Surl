namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// One <c>Part</c> of a multipart completion body: the part number and the <c>ETag</c> the
/// client says the part has.
/// </summary>
/// <param name="PartNumber">The part's number, as sent.</param>
/// <param name="ETag">The part's ETag, as sent.</param>
internal sealed record GcsCompletedPart(int PartNumber, string ETag);
