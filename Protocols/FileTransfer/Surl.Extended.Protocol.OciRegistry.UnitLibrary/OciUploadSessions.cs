namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// The open upload sessions of one server, by UUID (ADR-0115, decision 7): at most
/// <see cref="MaxOpen"/>, and one untouched for <see cref="IdleLifetime"/> on the injected clock
/// is expired, its staged bytes discarded, the next time any session is looked up or added.
/// </summary>
/// <remarks>
/// A request takes its session out while it works on it and returns it after, so two requests
/// never touch one session at once: the second is answered <c>BLOB_UPLOAD_UNKNOWN</c>.
/// </remarks>
/// <param name="clock">The clock sessions expire on.</param>
internal sealed class OciUploadSessions(TimeProvider clock)
{
    /// <summary>
    /// The most sessions open at once; past it an opening is <c>429 TOOMANYREQUESTS</c>.
    /// </summary>
    public const int MaxOpen = 1000;

    /// <summary>
    /// How long a session may go untouched before it expires.
    /// </summary>
    public static readonly TimeSpan IdleLifetime = TimeSpan.FromHours(1);

    private readonly Dictionary<string, OciUploadSession> open = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <summary>
    /// Adds a newly opened session, touched now.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <returns><see langword="false"/> when <see cref="MaxOpen"/> sessions are already open; the caller then discards it.</returns>
    public async Task<bool> TryAddAsync(OciUploadSession session)
    {
        List<OciUploadSession> expired;
        bool added;
        lock (gate)
        {
            expired = RemoveExpired();
            added = open.Count < MaxOpen;
            if (added)
            {
                session.LastTouched = clock.GetUtcNow();
                open.Add(session.Id, session);
            }
        }

        await DisposeAllAsync(expired);

        return added;
    }

    /// <summary>
    /// Takes the session <paramref name="id"/> of <paramref name="repositoryName"/> out, for one
    /// request to work on.
    /// </summary>
    /// <param name="id">The session's UUID.</param>
    /// <param name="repositoryName">The repository the request names.</param>
    /// <param name="accountName">The account of the request's token, or <see langword="null"/> when anonymous.</param>
    /// <returns>The session, or <see langword="null"/> for <c>BLOB_UPLOAD_UNKNOWN</c>, including another account's session.</returns>
    public async Task<OciUploadSession?> TakeAsync(string id, string repositoryName, string? accountName)
    {
        List<OciUploadSession> expired;
        OciUploadSession? taken = null;
        lock (gate)
        {
            expired = RemoveExpired();
            if (open.TryGetValue(id, out var session) && session.RepositoryName == repositoryName && session.OwnerAccountName == accountName)
            {
                open.Remove(id);
                taken = session;
            }
        }

        await DisposeAllAsync(expired);

        return taken;
    }

    /// <summary>
    /// Puts a taken session back, touched now.
    /// </summary>
    /// <param name="session">The session.</param>
    public void Return(OciUploadSession session)
    {
        lock (gate)
        {
            session.LastTouched = clock.GetUtcNow();
            open[session.Id] = session;
        }
    }

    private static async Task DisposeAllAsync(List<OciUploadSession> sessions)
    {
        foreach (var session in sessions)
        {
            await session.DisposeAsync();
        }
    }

    private List<OciUploadSession> RemoveExpired()
    {
        var now = clock.GetUtcNow();
        var expired = open.Values.Where(session => now - session.LastTouched >= IdleLifetime).ToList();
        foreach (var session in expired)
        {
            open.Remove(session.Id);
        }

        return expired;
    }
}
