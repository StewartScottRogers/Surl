namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The two ways an object is uploaded in more than one request (ADR-0110, decision 9).
/// </summary>
internal enum GcsUploadKind
{
    /// <summary>
    /// An XML API multipart upload: numbered parts, assembled at completion ("XML API multipart uploads").
    /// </summary>
    Multipart,

    /// <summary>
    /// A resumable upload: bytes appended in order through the session URI ("Resumable uploads").
    /// </summary>
    Resumable,
}
