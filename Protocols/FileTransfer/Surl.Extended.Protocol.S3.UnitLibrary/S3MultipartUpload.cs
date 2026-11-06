using Surl.Content;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// One open multipart upload (ADR-0108, decisions 1, 3 and 7): its ID, the bucket and key it was
/// created for, the fields CreateMultipartUpload named the object's metadata with, and the parts
/// uploaded so far, whose bytes sit one after another in an upload session opened at the key and
/// never committed - a hidden dot-file beside the object, so nothing is visible until
/// CompleteMultipartUpload writes the object through a fresh session.
/// </summary>
/// <param name="uploadId">The upload's ID: 32 lower-case hexadecimal digits.</param>
/// <param name="target">The bucket and key the upload was created for.</param>
/// <param name="metadataFields">The CreateMultipartUpload request's fields, kept for the completed object's metadata.</param>
/// <param name="session">The session the parts' bytes are written into.</param>
/// <param name="lastTouched">When the upload was created.</param>
internal sealed class S3MultipartUpload(string uploadId, S3RequestTarget target, IReadOnlyList<KeyValuePair<string, string>> metadataFields, ContentUploadSession session, DateTimeOffset lastTouched) : IAsyncDisposable
{
    private readonly Dictionary<int, S3UploadedPart> parts = [];

    /// <summary>
    /// The upload's ID.
    /// </summary>
    public string UploadId => uploadId;

    /// <summary>
    /// The CreateMultipartUpload request's fields.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> MetadataFields => metadataFields;

    /// <summary>
    /// The session the parts' bytes are in.
    /// </summary>
    public ContentUploadSession Session => session;

    /// <summary>
    /// When a request last created, wrote to or tried to complete the upload.
    /// </summary>
    public DateTimeOffset LastTouched { get; private set; } = lastTouched;

    /// <summary>
    /// Whether <paramref name="other"/> names the bucket and key the upload was created for, as
    /// sent, percent-encoding and all.
    /// </summary>
    /// <param name="other">A request's bucket and key.</param>
    /// <returns>Whether they are the upload's.</returns>
    public bool IsFor(S3RequestTarget other) => other.RawBucket == target.RawBucket && other.RawKey == target.RawKey;

    /// <summary>
    /// The part uploaded with <paramref name="partNumber"/>, or <see langword="null"/>.
    /// </summary>
    /// <param name="partNumber">The part number.</param>
    /// <returns>The part.</returns>
    public S3UploadedPart? Part(int partNumber) => parts.GetValueOrDefault(partNumber);

    /// <summary>
    /// Records an uploaded part, replacing one with the same number ("UploadPart": "the previously
    /// uploaded part is overwritten"); the old part's bytes stay in the session until the upload ends.
    /// </summary>
    /// <param name="partNumber">The part number.</param>
    /// <param name="part">Where the part's bytes are, and their MD5.</param>
    /// <param name="now">When.</param>
    public void Record(int partNumber, S3UploadedPart part, DateTimeOffset now)
    {
        parts[partNumber] = part;
        Touch(now);
    }

    /// <summary>
    /// Marks the upload as used now, so it is not aborted as idle.
    /// </summary>
    /// <param name="now">When.</param>
    public void Touch(DateTimeOffset now) => LastTouched = now;

    /// <summary>
    /// Discards the parts' bytes.
    /// </summary>
    /// <returns>A task that completes when the session is disposed.</returns>
    public ValueTask DisposeAsync() => session.DisposeAsync();
}
