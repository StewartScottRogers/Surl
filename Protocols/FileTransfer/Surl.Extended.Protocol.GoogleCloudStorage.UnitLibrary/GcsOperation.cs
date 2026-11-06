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

    /// <summary>
    /// <c>POST /&lt;bucket&gt;/&lt;object&gt;?uploads</c>: initiate a multipart upload (decision 9).
    /// </summary>
    InitiateMultipartUpload,

    /// <summary>
    /// <c>PUT /&lt;bucket&gt;/&lt;object&gt;?partNumber=&lt;n&gt;&amp;uploadId=&lt;id&gt;</c>: upload a part.
    /// </summary>
    UploadPart,

    /// <summary>
    /// <c>POST /&lt;bucket&gt;/&lt;object&gt;?uploadId=&lt;id&gt;</c>: complete a multipart upload.
    /// </summary>
    CompleteMultipartUpload,

    /// <summary>
    /// <c>DELETE /&lt;bucket&gt;/&lt;object&gt;?uploadId=&lt;id&gt;</c>: abort a multipart upload.
    /// </summary>
    AbortMultipartUpload,

    /// <summary>
    /// <c>GET /&lt;bucket&gt;/&lt;object&gt;?uploadId=&lt;id&gt;</c>, with <c>max-parts</c> and
    /// <c>part-number-marker</c> optional: list a multipart upload's parts.
    /// </summary>
    ListParts,

    /// <summary>
    /// <c>POST /&lt;bucket&gt;/&lt;object&gt;</c>, no query, <c>x-goog-resumable: start</c>: start a resumable upload.
    /// </summary>
    StartResumableUpload,

    /// <summary>
    /// <c>PUT /&lt;bucket&gt;/&lt;object&gt;?upload_id=&lt;id&gt;</c>: a resumable upload's chunk or status check.
    /// </summary>
    ResumeUpload,

    /// <summary>
    /// <c>DELETE /&lt;bucket&gt;/&lt;object&gt;?upload_id=&lt;id&gt;</c>: cancel a resumable upload.
    /// </summary>
    CancelResumableUpload,
}
