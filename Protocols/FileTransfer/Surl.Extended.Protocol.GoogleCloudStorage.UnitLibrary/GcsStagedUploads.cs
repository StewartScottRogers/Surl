namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The multipart and resumable uploads a server holds open, shared by every connection (ADR-0110,
/// decision 9): at most <see cref="MaxOpenUploads"/>, and one untouched for
/// <see cref="IdleLifetime"/> discarded the next time any upload request is served.
/// </summary>
/// <remarks>
/// Every use runs inside <see cref="RunAsync{T}"/>, one at a time, because an upload session is not
/// safe for concurrent use: the upload requests are served one after another across the server.
/// </remarks>
/// <param name="timeProvider">The clock an upload's idleness is measured on.</param>
internal sealed class GcsStagedUploads(TimeProvider timeProvider)
{
    /// <summary>
    /// The most uploads open at once; the next one is <c>503 SlowDown</c>.
    /// </summary>
    public const int MaxOpenUploads = 1000;

    /// <summary>
    /// How long an upload may go untouched before it is discarded.
    /// </summary>
    public static readonly TimeSpan IdleLifetime = TimeSpan.FromHours(24);

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, GcsStagedUpload> open = new(StringComparer.Ordinal);

    /// <summary>
    /// The clock.
    /// </summary>
    public TimeProvider TimeProvider => timeProvider;

    /// <summary>
    /// Whether another upload may be opened.
    /// </summary>
    public bool HasRoom => open.Count < MaxOpenUploads;

    /// <summary>
    /// Waits until no other request uses the uploads, discards each one idle past
    /// <see cref="IdleLifetime"/>, then runs <paramref name="use"/>.
    /// </summary>
    /// <typeparam name="T">What the use returns.</typeparam>
    /// <param name="use">The use.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>What the use returned.</returns>
    public async Task<T> RunAsync<T>(Func<Task<T>> use, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow();
            foreach (var idle in open.Values.Where(upload => now - upload.LastTouched > IdleLifetime).ToList())
            {
                await RemoveAsync(idle);
            }

            return await use();
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// The open upload <paramref name="uploadId"/> names, when it is of <paramref name="kind"/>
    /// and was opened for <paramref name="target"/>'s bucket and object; else <see langword="null"/>.
    /// </summary>
    /// <param name="uploadId">The upload ID the request sent.</param>
    /// <param name="kind">Whether a multipart or a resumable upload is meant.</param>
    /// <param name="target">The request's bucket and object.</param>
    /// <returns>The upload, or <see langword="null"/>.</returns>
    public GcsStagedUpload? Find(string uploadId, GcsUploadKind kind, GcsRequestTarget target) =>
        open.GetValueOrDefault(uploadId) is { } upload && upload.Kind == kind && upload.IsFor(target) ? upload : null;

    /// <summary>
    /// Holds <paramref name="upload"/> open; the caller checked <see cref="HasRoom"/>.
    /// </summary>
    /// <param name="upload">The new upload.</param>
    public void Add(GcsStagedUpload upload) => open.Add(upload.UploadId, upload);

    /// <summary>
    /// Ends <paramref name="upload"/>, discarding its staged bytes unless they were committed.
    /// </summary>
    /// <param name="upload">The upload.</param>
    /// <returns>A task that completes when the bytes are discarded.</returns>
    public async Task RemoveAsync(GcsStagedUpload upload)
    {
        open.Remove(upload.UploadId);
        await upload.DisposeAsync();
    }
}
