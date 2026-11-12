using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The blocks of one blob (ADR-0109, decisions 1 and 7): the uncommitted blocks Put Block staged,
/// whose bytes sit one after another in an upload session that is never committed - a hidden
/// dot-file beside the blob, so the blob is not visible until a block list commits it - and the
/// committed block list the last Put Block List wrote, remembered while the blob is the file it
/// wrote.
/// </summary>
/// <param name="lastTouched">When the staging began.</param>
internal sealed class AzureStagedBlocks(DateTimeOffset lastTouched) : IAzureStaging
{
    /// <summary>
    /// The most uncommitted blocks a blob holds ("Put Block"); the next is <c>409 BlockCountExceedsLimit</c>.
    /// </summary>
    public const int MaxUncommittedBlocks = 100000;

    /// <summary>
    /// The most blocks a committed block list holds ("Put Block List"); more is <c>400 BlockListTooLong</c>.
    /// </summary>
    public const int MaxCommittedBlocks = 50000;

    /// <summary>
    /// What every blob's staging key starts with, before its container and name.
    /// </summary>
    public const string KeyPrefix = "blocks ";

    private readonly List<AzureBlock> uncommitted = [];
    private readonly Dictionary<string, int> uncommittedPositions = new(StringComparer.Ordinal);
    private List<AzureBlock> committed = [];
    private ContentFileStatus? committedFile;

    /// <summary>
    /// The upload session the uncommitted blocks' bytes are in, or null when none are staged.
    /// </summary>
    public ContentUploadSession? Session { get; private set; }

    /// <inheritdoc/>
    public DateTimeOffset LastTouched { get; private set; } = lastTouched;

    /// <summary>
    /// Whether uncommitted blocks are staged: a blob with only a committed list may be forgotten.
    /// </summary>
    public bool HoldsStagedBytes => Session is not null;

    /// <summary>
    /// The uncommitted blocks, in the order their IDs were first put.
    /// </summary>
    public IReadOnlyList<AzureBlock> Uncommitted => uncommitted;

    /// <summary>
    /// The staging key of a blob: <c>blocks </c> and its container and name, percent-decoded, so it
    /// never meets a Data Lake file's key.
    /// </summary>
    /// <param name="target">The blob.</param>
    /// <returns>The key.</returns>
    public static string KeyOf(AzureBlobTarget target) => KeyPrefix + AzurePathUpdate.KeyOf(target);

    /// <summary>
    /// Begins the blocks of the blob at <paramref name="key"/> in <paramref name="staging"/>, whose
    /// caller is inside <see cref="AzureStagingArea.EnterAsync"/>.
    /// </summary>
    /// <param name="staging">The server's staged uploads.</param>
    /// <param name="key">The blob's staging key.</param>
    /// <returns>The new blocks, or null when the staging area has no room.</returns>
    public static async Task<AzureStagedBlocks?> BeginAsync(AzureStagingArea staging, string key)
    {
        var blocks = new AzureStagedBlocks(staging.TimeProvider.GetUtcNow());

        return await staging.TryAddAsync(key, blocks) ? blocks : null;
    }

    /// <summary>
    /// The committed block list, when <paramref name="file"/> is still the file the last block list
    /// wrote; else none, because the blob was written another way since.
    /// </summary>
    /// <param name="file">The blob's file now, or null when there is none.</param>
    /// <returns>The committed blocks, at their offsets in the blob.</returns>
    public IReadOnlyList<AzureBlock> CommittedFor(ContentFileStatus? file) =>
        file is not null && file == committedFile ? committed : [];

    /// <summary>
    /// Starts the session the uncommitted blocks' bytes are written into.
    /// </summary>
    /// <param name="session">The session.</param>
    public void BeginSession(ContentUploadSession session) => Session = session;

    /// <summary>
    /// Records a block put into the session, replacing an uncommitted block with the same ID.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <param name="now">When.</param>
    public void Stage(AzureBlock block, DateTimeOffset now)
    {
        if (uncommittedPositions.TryGetValue(block.Id, out var existing))
        {
            uncommitted[existing] = block;
        }
        else
        {
            uncommittedPositions.Add(block.Id, uncommitted.Count);
            uncommitted.Add(block);
        }

        LastTouched = now;
    }

    /// <summary>
    /// Whether an uncommitted block has the ID <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The block ID.</param>
    /// <returns>Whether it is staged.</returns>
    public bool HoldsUncommitted(string id) => uncommittedPositions.ContainsKey(id);

    /// <summary>
    /// Records a committed block list and discards every uncommitted block, as "Put Block List" says
    /// the service does.
    /// </summary>
    /// <param name="blocks">The committed blocks, at their offsets in the blob.</param>
    /// <param name="file">The blob's file as the commit left it.</param>
    /// <param name="now">When.</param>
    /// <returns>A task that completes when the session is disposed.</returns>
    public async Task CommitAsync(IReadOnlyList<AzureBlock> blocks, ContentFileStatus file, DateTimeOffset now)
    {
        committed = [.. blocks];
        committedFile = file;
        LastTouched = now;
        await DiscardUncommittedAsync();
    }

    /// <summary>
    /// Discards every uncommitted block and the session holding their bytes.
    /// </summary>
    /// <returns>A task that completes when the session is disposed.</returns>
    public async Task DiscardUncommittedAsync()
    {
        uncommitted.Clear();
        uncommittedPositions.Clear();
        if (Session is { } session)
        {
            Session = null;
            await session.DisposeAsync();
        }
    }

    /// <summary>
    /// Discards the uncommitted blocks.
    /// </summary>
    /// <returns>A task that completes when the session is disposed.</returns>
    public ValueTask DisposeAsync() => new(DiscardUncommittedAsync());
}
