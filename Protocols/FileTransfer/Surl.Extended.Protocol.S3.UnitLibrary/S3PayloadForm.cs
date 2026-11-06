namespace Surl.Extended.Protocol.S3;

/// <summary>
/// How a write's body is framed, by its <c>x-amz-content-sha256</c> (ADR-0108 decisions 4.2 and 4.5).
/// </summary>
internal enum S3PayloadForm
{
    /// <summary>
    /// The object's bytes as they are: a hex SHA-256, <c>UNSIGNED-PAYLOAD</c>, or no field.
    /// </summary>
    Plain,

    /// <summary>
    /// <c>STREAMING-AWS4-HMAC-SHA256-PAYLOAD</c>: <c>aws-chunked</c>, every chunk signed.
    /// </summary>
    SignedChunks,

    /// <summary>
    /// <c>STREAMING-UNSIGNED-PAYLOAD-TRAILER</c>: <c>aws-chunked</c>, no chunk signed, a trailer after the last.
    /// </summary>
    UnsignedChunksWithTrailer,

    /// <summary>
    /// Any other <c>STREAMING-</c> value (ECDSA, the signed trailer forms): <c>501 NotImplemented</c>.
    /// </summary>
    Unserved,
}
