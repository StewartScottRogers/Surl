namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The JSON API operations the server serves (ADR-0148, decision 2's table, the reads and the delete).
/// </summary>
internal enum GcsJsonOperation
{
    /// <summary>
    /// Every other request on a JSON path: <c>501 notImplemented</c>.
    /// </summary>
    Unlisted,

    /// <summary>
    /// <c>GET /storage/v1/b?project=</c>: <c>storage.buckets.list</c>.
    /// </summary>
    ListBuckets,

    /// <summary>
    /// <c>GET /storage/v1/b/&lt;bucket&gt;</c>: <c>storage.buckets.get</c>.
    /// </summary>
    GetBucket,

    /// <summary>
    /// <c>GET /storage/v1/b/&lt;bucket&gt;/o</c>: <c>storage.objects.list</c>.
    /// </summary>
    ListObjects,

    /// <summary>
    /// <c>GET …/o/&lt;object&gt;</c>, the resource or <c>alt=media</c>: <c>storage.objects.get</c>.
    /// </summary>
    GetObject,

    /// <summary>
    /// <c>DELETE …/o/&lt;object&gt;</c>: <c>storage.objects.delete</c>.
    /// </summary>
    DeleteObject,
}
