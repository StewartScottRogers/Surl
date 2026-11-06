namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Which of the distribution specification's endpoints a request path names (Dist "Endpoints";
/// ADR-0115, decision 1).
/// </summary>
internal enum OciEndpoint
{
    /// <summary>
    /// No endpoint the server serves: <c>404 UNSUPPORTED</c>.
    /// </summary>
    None = 0,

    /// <summary>
    /// end-1, <c>/v2/</c>.
    /// </summary>
    Base,

    /// <summary>
    /// end-2, <c>/v2/&lt;name&gt;/blobs/&lt;digest&gt;</c>.
    /// </summary>
    Blob,

    /// <summary>
    /// end-3, <c>/v2/&lt;name&gt;/manifests/&lt;reference&gt;</c>.
    /// </summary>
    Manifest,

    /// <summary>
    /// end-4 and end-11, <c>/v2/&lt;name&gt;/blobs/uploads/</c>: opening an upload, a single
    /// <c>POST</c>, or a mount.
    /// </summary>
    Uploads,

    /// <summary>
    /// end-5, end-6 and end-13, <c>/v2/&lt;name&gt;/blobs/uploads/&lt;reference&gt;</c>: one
    /// upload session.
    /// </summary>
    UploadSession,

    /// <summary>
    /// end-8, <c>/v2/&lt;name&gt;/tags/list</c>.
    /// </summary>
    TagsList,

    /// <summary>
    /// end-12, <c>/v2/&lt;name&gt;/referrers/&lt;digest&gt;</c>.
    /// </summary>
    Referrers,
}
