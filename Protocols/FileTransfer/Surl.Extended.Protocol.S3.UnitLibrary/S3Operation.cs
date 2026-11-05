namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The operations the server serves, recognised by method, by whether the request names a bucket
/// and a key, and by its query (ADR-0108, decision 1's table).
/// </summary>
internal enum S3Operation
{
    /// <summary>
    /// Every request decision 1 does not serve: <c>501 NotImplemented</c>.
    /// </summary>
    NotImplemented,

    /// <summary>
    /// <c>GET /&lt;bucket&gt;/&lt;key&gt;</c> or <c>HEAD</c>, no query: GetObject and HeadObject.
    /// </summary>
    ReadObject,

    /// <summary>
    /// <c>PUT /&lt;bucket&gt;/&lt;key&gt;</c>, no query and no <c>x-amz-copy-source</c>: PutObject.
    /// </summary>
    PutObject,

    /// <summary>
    /// <c>GET /&lt;bucket&gt;?list-type=2</c>: ListObjectsV2.
    /// </summary>
    ListObjectsV2,
}
