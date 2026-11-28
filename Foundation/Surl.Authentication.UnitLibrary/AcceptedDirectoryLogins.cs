namespace Surl.Authentication;

/// <summary>
/// When each name's last password login was accepted by the LDAP directory through one
/// <see cref="AuthenticationPolicy"/>, so a token issued to a directory user is honoured for
/// <see cref="HonouredFor"/> after it (ADR-0167, Amendment 3, points 2 and 6). It holds names and
/// times only, no secret, at most <see cref="MaxNames"/> names, in memory.
/// </summary>
/// <param name="timeProvider">The clock a login's time is read from.</param>
internal sealed class AcceptedDirectoryLogins(TimeProvider timeProvider)
{
    /// <summary>The names recorded at once.</summary>
    public const int MaxNames = 10000;

    /// <summary>How long after its last accepted login a name is still recently accepted.</summary>
    public static readonly TimeSpan HonouredFor = TimeSpan.FromHours(1);

    private readonly Lock gate = new();
    private readonly Dictionary<string, DateTimeOffset> lastAcceptedAt = new(StringComparer.Ordinal);

    /// <summary>
    /// Records that a login of <paramref name="userName"/> is accepted now. A name not yet recorded,
    /// when <see cref="MaxNames"/> are, first drops every name no longer recently accepted, and then,
    /// if none was, the name whose last login is oldest.
    /// </summary>
    /// <param name="userName">The name as the client gave it, compared ordinally.</param>
    public void Record(string userName)
    {
        lock (gate)
        {
            var now = timeProvider.GetUtcNow();
            if (lastAcceptedAt.Count == MaxNames && !lastAcceptedAt.ContainsKey(userName))
            {
                MakeRoom(now);
            }

            lastAcceptedAt[userName] = now;
        }
    }

    /// <summary>
    /// Whether a login of <paramref name="userName"/> was recorded less than <see cref="HonouredFor"/> ago.
    /// </summary>
    /// <param name="userName">The name, compared ordinally.</param>
    /// <returns><see langword="true"/> when it was.</returns>
    public bool IsRecentlyAccepted(string userName)
    {
        lock (gate)
        {
            return lastAcceptedAt.TryGetValue(userName, out var acceptedAt) && IsRecent(acceptedAt, timeProvider.GetUtcNow());
        }
    }

    private static bool IsRecent(DateTimeOffset acceptedAt, DateTimeOffset now) => now - acceptedAt < HonouredFor;

    private void MakeRoom(DateTimeOffset now)
    {
        var outdated = lastAcceptedAt.Where(login => !IsRecent(login.Value, now)).Select(login => login.Key).ToList();
        foreach (var userName in outdated)
        {
            lastAcceptedAt.Remove(userName);
        }

        if (lastAcceptedAt.Count == MaxNames)
        {
            lastAcceptedAt.Remove(lastAcceptedAt.MinBy(login => login.Value).Key);
        }
    }
}
