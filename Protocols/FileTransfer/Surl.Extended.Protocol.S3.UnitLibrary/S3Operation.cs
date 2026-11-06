namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The operations the server serves, recognised by method, by whether the request names the
/// service, a bucket or a key, and by its query (ADR-0108, decision 1's table).
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
    /// <c>DELETE /&lt;bucket&gt;/&lt;key&gt;</c>, no query: DeleteObject.
    /// </summary>
    DeleteObject,

    /// <summary>
    /// <c>GET /&lt;bucket&gt;?list-type=2</c>: ListObjectsV2.
    /// </summary>
    ListObjectsV2,

    /// <summary>
    /// <c>HEAD /&lt;bucket&gt;</c>, no query: HeadBucket.
    /// </summary>
    HeadBucket,

    /// <summary>
    /// <c>GET /</c> on the service, no query: ListBuckets.
    /// </summary>
    ListBuckets,
}
