namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The uploads a server holds staged and not yet committed, shared by every connection (ADR-0109,
/// decisions 5.5 and 7): one <see cref="AzureStagedPath"/> per Data Lake file with appends not yet
/// flushed, and one <see cref="AzureStagedBlocks"/> per blob with blocks put or a block list
/// committed. At most <see cref="MaxStagings"/>, and one untouched for <see cref="StagedLifetime"/>
/// discarded when the next staging begins.
/// </summary>
/// <remarks>
/// Every use runs inside <see cref="EnterAsync"/> and <see cref="Exit"/>, one at a time, because an
/// upload session is not safe for concurrent use: appends, flushes and block requests are served
/// one after another across the server.
/// </remarks>
/// <param name="timeProvider">The clock a staging's age is measured on.</param>
internal sealed class AzureStagingArea(TimeProvider timeProvider)
{
    /// <summary>
    /// The most files and blobs with a staging at once ("1000 blobs with staged blocks or appends
    /// per server", decision 7); the next is <c>503 ServerBusy</c>, unless a staging that holds no
    /// staged bytes can be forgotten to make room.
    /// </summary>
    public const int MaxStagings = 1000;

    /// <summary>
    /// How long a staging may go untouched before it is discarded: 7 days, as "Put Block" keeps an
    /// uncommitted block list.
    /// </summary>
    public static readonly TimeSpan StagedLifetime = TimeSpan.FromDays(7);

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, IAzureStaging> staged = new(StringComparer.Ordinal);

    /// <summary>
    /// The clock.
    /// </summary>
    public TimeProvider TimeProvider => timeProvider;

    /// <summary>
    /// Waits until no other connection uses the staging area.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the caller may use it.</returns>
    public Task EnterAsync(CancellationToken cancellationToken) => gate.WaitAsync(cancellationToken);

    /// <summary>
    /// Lets the next connection use the staging area.
    /// </summary>
    public void Exit() => gate.Release();

    /// <summary>
    /// The staging at <paramref name="key"/> when it is a <typeparamref name="TStaging"/>, or null.
    /// </summary>
    /// <typeparam name="TStaging">The kind of staging looked for.</typeparam>
    /// <param name="key">The staging's key.</param>
    /// <returns>The staging, or null.</returns>
    public TStaging? Find<TStaging>(string key)
        where TStaging : class, IAzureStaging => staged.GetValueOrDefault(key) as TStaging;

    /// <summary>
    /// Begins a staging at <paramref name="key"/>, after discarding each staging past
    /// <see cref="StagedLifetime"/> and, when <see cref="MaxStagings"/> are held, forgetting the
    /// least recently touched one that holds no staged bytes.
    /// </summary>
    /// <param name="key">The staging's key.</param>
    /// <param name="staging">The new staging.</param>
    /// <returns>Whether it was added: false when <see cref="MaxStagings"/> stagings hold staged
    /// bytes, and the caller then disposes the new one.</returns>
    public async Task<bool> TryAddAsync(string key, IAzureStaging staging)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var expired in staged.Where(entry => now - entry.Value.LastTouched > StagedLifetime).Select(entry => entry.Key).ToList())
        {
            await DiscardAsync(expired);
        }

        if (!await MakeRoomAsync())
        {
            return false;
        }

        staged.Add(key, staging);
        return true;
    }

    // Below the limit there is room; at it, the least recently touched idle staging is forgotten.
    private async Task<bool> MakeRoomAsync()
    {
        if (staged.Count < MaxStagings)
        {
            return true;
        }

        var idle = staged.Where(entry => !entry.Value.HoldsStagedBytes).OrderBy(entry => entry.Value.LastTouched).Select(entry => entry.Key).FirstOrDefault();
        if (idle is null)
        {
            return false;
        }

        await DiscardAsync(idle);
        return true;
    }

    /// <summary>
    /// Waits its turn, then ends the staging at <paramref name="key"/>, if it has one: for a caller
    /// outside <see cref="EnterAsync"/>, such as Path - Create and Path - Delete.
    /// </summary>
    /// <param name="key">The staging's key.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the staging is discarded.</returns>
    public async Task DiscardInTurnAsync(string key, CancellationToken cancellationToken)
    {
        await EnterAsync(cancellationToken);
        try
        {
            await DiscardAsync(key);
        }
        finally
        {
            Exit();
        }
    }

    /// <summary>
    /// Ends the staging at <paramref name="key"/>, if it has one, discarding its uncommitted bytes.
    /// The caller is inside <see cref="EnterAsync"/>.
    /// </summary>
    /// <param name="key">The staging's key.</param>
    /// <returns>A task that completes when the staging is disposed.</returns>
    public async Task DiscardAsync(string key)
    {
        if (staged.Remove(key, out var staging))
        {
            await staging.DisposeAsync();
        }
    }
}
