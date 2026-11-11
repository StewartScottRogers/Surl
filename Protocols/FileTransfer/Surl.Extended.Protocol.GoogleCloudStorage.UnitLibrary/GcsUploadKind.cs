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

    /// <summary>
    /// A JSON API resumable upload (ADR-0148, decision 7): the same bytes-in-order session, but its own grammar, so an XML
    /// upload ID is no session of the JSON URL and the reverse.
    /// </summary>
    JsonResumable,
}
