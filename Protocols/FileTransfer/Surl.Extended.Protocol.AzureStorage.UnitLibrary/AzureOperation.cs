namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Blob endpoint operations the server serves (ADR-0109, decision 1).
/// </summary>
internal enum AzureOperation
{
    /// <summary>
    /// Get Blob or Get Blob Properties: <c>GET</c> or <c>HEAD /&lt;container&gt;/&lt;blob&gt;</c>.
    /// </summary>
    ReadBlob,

    /// <summary>
    /// Put Blob: <c>PUT /&lt;container&gt;/&lt;blob&gt;</c> with <c>x-ms-blob-type: BlockBlob</c>.
    /// </summary>
    PutBlob,

    /// <summary>
    /// Delete Blob: <c>DELETE /&lt;container&gt;/&lt;blob&gt;</c>.
    /// </summary>
    DeleteBlob,

    /// <summary>
    /// List Blobs: <c>GET /&lt;container&gt;?restype=container&amp;comp=list</c>.
    /// </summary>
    ListBlobs,
}
