namespace Surl.Protocol.HttpProxy;

/// <summary>
/// How copying a chunked body ended.
/// </summary>
internal enum ChunkedCopyOutcome
{
    /// <summary>Every chunk and the last chunk were copied.</summary>
    Copied,

    /// <summary>A chunk line or chunk was malformed, or the sender closed before the body ended.</summary>
    Malformed,

    /// <summary>The chunks held more data bytes than the limit allows.</summary>
    TooLarge,
}
