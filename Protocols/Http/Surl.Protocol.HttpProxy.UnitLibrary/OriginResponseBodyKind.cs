namespace Surl.Protocol.HttpProxy;

/// <summary>
/// How an origin's response body is framed.
/// </summary>
internal enum OriginResponseBodyKind
{
    /// <summary>The response has no body.</summary>
    None,

    /// <summary>The body is <c>Content-Length</c> bytes long.</summary>
    Length,

    /// <summary>The body is chunked.</summary>
    Chunked,

    /// <summary>The body ends when the origin closes its connection.</summary>
    UntilClose,

    /// <summary>The <c>Content-Length</c> fields are malformed or disagree: the response cannot be relayed.</summary>
    Invalid,
}
