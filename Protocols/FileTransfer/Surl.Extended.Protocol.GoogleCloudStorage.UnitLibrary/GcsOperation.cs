namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The operations the server serves, recognised by method, by whether the request names a bucket
/// and an object, and by its query (ADR-0110, decision 1's table).
/// </summary>
internal enum GcsOperation
{
    /// <summary>
    /// Every request decision 1 does not list: <c>400 NotImplemented</c>.
    /// </summary>
    NotImplemented,

    /// <summary>
    /// <c>GET /</c> on the service: List Buckets.
    /// </summary>
    ListBuckets,

    /// <summary>
    /// <c>GET /&lt;bucket&gt;</c>, either listing version: List Objects.
    /// </summary>
    ListObjects,

    /// <summary>
    /// <c>GET</c> or <c>HEAD /&lt;bucket&gt;/&lt;object&gt;</c>: GET Object and HEAD Object.
    /// </summary>
    ReadObject,

    /// <summary>
    /// <c>PUT /&lt;bucket&gt;/&lt;object&gt;</c>, no query and no <c>x-goog-copy-source</c>: PUT Object.
    /// </summary>
    PutObject,

    /// <summary>
    /// <c>DELETE /&lt;bucket&gt;/&lt;object&gt;</c>, no query: DELETE Object.
    /// </summary>
    DeleteObject,
}
