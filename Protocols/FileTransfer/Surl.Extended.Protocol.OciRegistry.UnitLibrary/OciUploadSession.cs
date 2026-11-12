using System.Security.Cryptography;
using Surl.Content;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// One blob upload session (Dist "Push"; ADR-0115, decision 5.4): the bytes received so far,
/// staged in a content store upload session inside the repository's <c>blobs</c> folder, and
/// their <c>sha256</c> and <c>sha512</c> hashes, kept as they arrive so completion never reads the
/// bytes back.
/// </summary>
/// <remarks>
/// Chunks only ever append, so the running hashes are the hashes of everything staged. Disposing
/// the session discards its staged bytes.
/// </remarks>
internal sealed class OciUploadSession : IAsyncDisposable
{
    private readonly IncrementalHash sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly IncrementalHash sha512 = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);

    /// <summary>
    /// Creates the session.
    /// </summary>
    /// <param name="id">The session's UUID, the last segment of its <c>Location</c>.</param>
    /// <param name="repositoryName">The repository it uploads to.</param>
    /// <param name="stagingPath">The request path of the file the staged bytes are committed to.</param>
    /// <param name="staging">The content store's upload session holding the bytes.</param>
    public OciUploadSession(string id, string repositoryName, string stagingPath, ContentUploadSession staging)
    {
        Id = id;
        RepositoryName = repositoryName;
        StagingPath = stagingPath;
        Staging = staging;
    }

    /// <summary>
    /// The session's UUID.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The repository the session uploads to; it answers no other.
    /// </summary>
    public string RepositoryName { get; }

    /// <summary>
    /// The account whose token opened the session (<see langword="null"/> for an anonymous token);
    /// a token of another account is answered <c>BLOB_UPLOAD_UNKNOWN</c> (ADR-0115, code-3).
    /// </summary>
    public string? OwnerAccountName { get; set; }

    /// <summary>
    /// The request path the staged bytes are committed to before they are renamed into place.
    /// </summary>
    public string StagingPath { get; }

    /// <summary>
    /// The content store's upload session holding the bytes.
    /// </summary>
    public ContentUploadSession Staging { get; }

    /// <summary>
    /// When the session was last opened or touched; <see cref="OciUploadSessions.IdleLifetime"/>
    /// later it expires.
    /// </summary>
    public DateTimeOffset LastTouched { get; set; }

    /// <summary>
    /// How many bytes are staged.
    /// </summary>
    public long Length => Staging.Length;

    /// <summary>
    /// The session's <c>Location</c>, <c>/v2/&lt;name&gt;/blobs/uploads/&lt;id&gt;</c>.
    /// </summary>
    public string Location => $"/v2/{RepositoryName}/blobs/uploads/{Id}";

    /// <summary>
    /// The <c>Range</c> an answer carries: <c>0-&lt;last byte offset held&gt;</c>, and <c>0-0</c>
    /// for an empty session, as the distribution reference implementation sends it.
    /// </summary>
    public string Range => $"0-{Math.Max(Length - 1, 0)}";

    /// <summary>
    /// Appends bytes and hashes them.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns><see langword="false"/> when the upload went past <c>--max-filesize</c>, which discards it.</returns>
    public async Task<bool> AppendAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        sha256.AppendData(bytes.Span);
        sha512.AppendData(bytes.Span);

        return await Staging.WriteAtAsync(Staging.Length, bytes, cancellationToken) == ContentUploadResult.Written;
    }

    /// <summary>
    /// Whether everything staged hashes to <paramref name="digest"/>.
    /// </summary>
    /// <param name="digest">The digest the client named.</param>
    /// <returns><see langword="true"/> when it matches.</returns>
    public bool IsHashedTo(OciDigest digest) =>
        digest.IsHashOf((digest.Algorithm == "sha256" ? sha256 : sha512).GetCurrentHash());

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        sha256.Dispose();
        sha512.Dispose();
        await Staging.DisposeAsync();
    }
}
