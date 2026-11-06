using Surl.Content;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// One open multipart or resumable upload (ADR-0110, decision 9): its ID, the bucket and object it
/// was opened for, the fields the opening request named the object's metadata with, and the bytes
/// staged so far in an upload session at the object - a hidden dot-file beside it, so nothing is
/// visible until the upload completes. A multipart upload also records its parts, one after another
/// in the session; a resumable upload's session holds the object's bytes in order.
/// </summary>
/// <param name="uploadId">The upload's ID: 32 lower-case hexadecimal digits.</param>
/// <param name="kind">Multipart or resumable.</param>
/// <param name="target">The bucket and object the upload was opened for.</param>
/// <param name="metadataFields">The opening request's fields, kept for the object's metadata.</param>
/// <param name="session">The session the bytes are staged in.</param>
/// <param name="lastTouched">When the upload was opened.</param>
internal sealed class GcsStagedUpload(string uploadId, GcsUploadKind kind, GcsRequestTarget target, IReadOnlyList<KeyValuePair<string, string>> metadataFields, ContentUploadSession session, DateTimeOffset lastTouched) : IAsyncDisposable
{
    private readonly SortedDictionary<int, GcsUploadedPart> parts = [];

    /// <summary>
    /// The upload's ID.
    /// </summary>
    public string UploadId => uploadId;

    /// <summary>
    /// Multipart or resumable.
    /// </summary>
    public GcsUploadKind Kind => kind;

    /// <summary>
    /// The opening request's fields.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> MetadataFields => metadataFields;

    /// <summary>
    /// The session the bytes are staged in.
    /// </summary>
    public ContentUploadSession Session => session;

    /// <summary>
    /// When a request last opened, wrote to or tried to complete the upload.
    /// </summary>
    public DateTimeOffset LastTouched { get; private set; } = lastTouched;

    /// <summary>
    /// The parts uploaded so far, in part-number order.
    /// </summary>
    public IEnumerable<KeyValuePair<int, GcsUploadedPart>> Parts => parts;

    /// <summary>
    /// Whether <paramref name="other"/> names the bucket and object the upload was opened for, as sent.
    /// </summary>
    /// <param name="other">A request's bucket and object.</param>
    /// <returns>Whether they are the upload's.</returns>
    public bool IsFor(GcsRequestTarget other) => other.RawBucket == target.RawBucket && other.RawName == target.RawName;

    /// <summary>
    /// The part uploaded with <paramref name="partNumber"/>, or <see langword="null"/>.
    /// </summary>
    /// <param name="partNumber">The part number.</param>
    /// <returns>The part.</returns>
    public GcsUploadedPart? Part(int partNumber) => parts.GetValueOrDefault(partNumber);

    /// <summary>
    /// Records an uploaded part, replacing one with the same number; the old part's bytes stay in
    /// the session until the upload ends.
    /// </summary>
    /// <param name="partNumber">The part number.</param>
    /// <param name="part">Where the part's bytes are, and their MD5.</param>
    public void Record(int partNumber, GcsUploadedPart part)
    {
        parts[partNumber] = part;
        Touch(part.Uploaded);
    }

    /// <summary>
    /// Marks the upload as used now, so it is not discarded as idle.
    /// </summary>
    /// <param name="now">When.</param>
    public void Touch(DateTimeOffset now) => LastTouched = now;

    /// <summary>
    /// Discards the staged bytes, unless the session was committed.
    /// </summary>
    /// <returns>A task that completes when the session is disposed.</returns>
    public ValueTask DisposeAsync() => session.DisposeAsync();
}
