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

    /// <summary>
    /// Put Block: <c>PUT /&lt;container&gt;/&lt;blob&gt;?comp=block&amp;blockid=&lt;id&gt;</c>.
    /// </summary>
    PutBlock,

    /// <summary>
    /// Put Block List: <c>PUT /&lt;container&gt;/&lt;blob&gt;?comp=blocklist</c>.
    /// </summary>
    PutBlockList,

    /// <summary>
    /// Get Block List: <c>GET /&lt;container&gt;/&lt;blob&gt;?comp=blocklist</c>.
    /// </summary>
    GetBlockList,
}
