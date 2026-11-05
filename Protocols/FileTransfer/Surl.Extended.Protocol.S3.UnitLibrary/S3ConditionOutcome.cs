namespace Surl.Extended.Protocol.S3;

/// <summary>
/// What GetObject's and HeadObject's conditional fields decide (GetObject; ADR-0108, decision 1).
/// </summary>
internal enum S3ConditionOutcome
{
    /// <summary>
    /// Every condition holds, or none was sent: the object is served.
    /// </summary>
    Serve,

    /// <summary>
    /// <c>If-None-Match</c> matched, or <c>If-Modified-Since</c> found the object unmodified: <c>304</c>.
    /// </summary>
    NotModified,

    /// <summary>
    /// <c>If-Match</c> or <c>If-Unmodified-Since</c> failed: <c>412 PreconditionFailed</c>.
    /// </summary>
    PreconditionFailed,
}
