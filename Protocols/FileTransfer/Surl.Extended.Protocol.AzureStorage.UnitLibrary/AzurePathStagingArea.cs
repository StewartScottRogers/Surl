namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Data Lake appends a server holds staged and not yet flushed, one <see cref="AzureStagedPath"/>
/// per file, shared by every connection (ADR-0109, decisions 5.5 and 7): at most
/// <see cref="MaxStagedPaths"/>, and one untouched for <see cref="StagedLifetime"/> discarded when
/// the next staging begins.
/// </summary>
/// <remarks>
/// Every use runs inside <see cref="EnterAsync"/> and <see cref="Exit"/>, one at a time, because an
/// upload session is not safe for concurrent use: appends and flushes are served one after another
/// across the server.
/// </remarks>
/// <param name="timeProvider">The clock a staging's age is measured on.</param>
internal sealed class AzurePathStagingArea(TimeProvider timeProvider)
{
    /// <summary>
    /// The most files with staged appends at once; the next is <c>503 ServerBusy</c>.
    /// </summary>
    public const int MaxStagedPaths = 1000;

    /// <summary>
    /// How long a staging may go untouched before it is discarded: 7 days, as "Put Block" keeps an
    /// uncommitted block list.
    /// </summary>
    public static readonly TimeSpan StagedLifetime = TimeSpan.FromDays(7);

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, AzureStagedPath> staged = new(StringComparer.Ordinal);

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
    /// The staging of the file at <paramref name="key"/>, or null when it has none.
    /// </summary>
    /// <param name="key">The file's filesystem and path, percent-decoded.</param>
    /// <returns>The staging, or null.</returns>
    public AzureStagedPath? Find(string key) => staged.GetValueOrDefault(key);

    /// <summary>
    /// Begins a staging for the file at <paramref name="key"/>, after discarding each staging past
    /// <see cref="StagedLifetime"/>.
    /// </summary>
    /// <param name="key">The file's filesystem and path, percent-decoded.</param>
    /// <param name="staging">The new staging.</param>
    /// <returns>Whether it was added: false when <see cref="MaxStagedPaths"/> are already held,
    /// and the caller then disposes its session.</returns>
    public async Task<bool> TryAddAsync(string key, AzureStagedPath staging)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var expired in staged.Where(entry => now - entry.Value.LastTouched > StagedLifetime).Select(entry => entry.Key).ToList())
        {
            await DiscardAsync(expired);
        }

        if (staged.Count >= MaxStagedPaths)
        {
            return false;
        }

        staged.Add(key, staging);
        return true;
    }

    /// <summary>
    /// Waits its turn, then ends the staging of the file at <paramref name="key"/>, if it has one:
    /// for a caller outside <see cref="EnterAsync"/>, such as Path - Create and Path - Delete.
    /// </summary>
    /// <param name="key">The file's filesystem and path, percent-decoded.</param>
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
    /// Ends the staging of the file at <paramref name="key"/>, if it has one, discarding its
    /// session's uncommitted bytes. The caller is inside <see cref="EnterAsync"/>.
    /// </summary>
    /// <param name="key">The file's filesystem and path, percent-decoded.</param>
    /// <returns>A task that completes when the session is disposed.</returns>
    public async Task DiscardAsync(string key)
    {
        if (staged.Remove(key, out var staging))
        {
            await staging.Session.DisposeAsync();
        }
    }
}
