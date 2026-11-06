namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The multipart uploads a server holds open, shared by every connection (ADR-0108, decision 7):
/// at most <see cref="MaxOpenUploads"/>, and one untouched for <see cref="IdleLifetime"/> aborted,
/// its parts discarded, the next time any multipart request is served.
/// </summary>
/// <remarks>
/// Every use runs between <see cref="EnterAsync"/> and <see cref="Exit"/>, one at a time, because
/// an upload session is not safe for concurrent use: the multipart requests are served one after
/// another across the server.
/// </remarks>
/// <param name="timeProvider">The clock an upload's idleness is measured on.</param>
internal sealed class S3MultipartUploads(TimeProvider timeProvider)
{
    /// <summary>
    /// The most uploads open at once; the next CreateMultipartUpload is <c>503 SlowDown</c>.
    /// </summary>
    public const int MaxOpenUploads = 1000;

    /// <summary>
    /// How long an upload may go untouched before it is aborted.
    /// </summary>
    public static readonly TimeSpan IdleLifetime = TimeSpan.FromHours(24);

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, S3MultipartUpload> open = new(StringComparer.Ordinal);

    /// <summary>
    /// The clock.
    /// </summary>
    public TimeProvider TimeProvider => timeProvider;

    /// <summary>
    /// Whether another upload may be opened.
    /// </summary>
    public bool HasRoom => open.Count < MaxOpenUploads;

    /// <summary>
    /// Waits until no other request uses the uploads, then aborts each one idle past
    /// <see cref="IdleLifetime"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the caller may use the uploads.</returns>
    public async Task EnterAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        foreach (var idle in open.Values.Where(upload => now - upload.LastTouched > IdleLifetime).ToList())
        {
            await RemoveAsync(idle);
        }
    }

    /// <summary>
    /// Lets the next request use the uploads.
    /// </summary>
    public void Exit() => gate.Release();

    /// <summary>
    /// The open upload <paramref name="uploadId"/> names, when it was created for
    /// <paramref name="target"/>'s bucket and key; else <see langword="null"/>.
    /// </summary>
    /// <param name="uploadId">The <c>uploadId</c> the request sent.</param>
    /// <param name="target">The request's bucket and key.</param>
    /// <returns>The upload, or <see langword="null"/>.</returns>
    public S3MultipartUpload? Find(string uploadId, S3RequestTarget target) =>
        open.GetValueOrDefault(uploadId) is { } upload && upload.IsFor(target) ? upload : null;

    /// <summary>
    /// Holds <paramref name="upload"/> open; the caller checked <see cref="HasRoom"/>.
    /// </summary>
    /// <param name="upload">The new upload.</param>
    public void Add(S3MultipartUpload upload) => open.Add(upload.UploadId, upload);

    /// <summary>
    /// Ends <paramref name="upload"/>, discarding its parts' bytes.
    /// </summary>
    /// <param name="upload">The upload.</param>
    /// <returns>A task that completes when the parts are discarded.</returns>
    public async Task RemoveAsync(S3MultipartUpload upload)
    {
        open.Remove(upload.UploadId);
        await upload.DisposeAsync();
    }
}
