namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// What Get Blob's and Get Blob Properties' conditional fields decide ("Specifying conditional headers for Blob service operations"; ADR-0109, decision 1).
/// </summary>
internal enum AzureConditionOutcome
{
    /// <summary>
    /// Every condition holds, or none was sent: the blob is served.
    /// </summary>
    Serve,

    /// <summary>
    /// <c>If-None-Match</c> matched, or <c>If-Modified-Since</c> found the blob unmodified: <c>304</c>.
    /// </summary>
    NotModified,

    /// <summary>
    /// <c>If-Match</c> or <c>If-Unmodified-Since</c> failed: <c>412 ConditionNotMet</c>.
    /// </summary>
    ConditionNotMet,
}
