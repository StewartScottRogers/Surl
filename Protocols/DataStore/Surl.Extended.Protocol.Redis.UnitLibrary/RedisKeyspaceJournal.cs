using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The changes the command now running has made to the keyspace, in the order each key was first
/// touched, one per key with its last change (an <c>MSET</c> of three keys is three; a key written
/// twice is one). Callers hold <see cref="RedisKeyspace.SyncRoot"/>.
/// </summary>
internal sealed class RedisKeyspaceJournal
{
    private readonly List<RedisKeyspaceChange> changes = [];

    /// <summary>Notes that the key was written, its value now <paramref name="byteCount"/> bytes.</summary>
    /// <param name="database">The database, 0 to 15.</param>
    /// <param name="key">The key.</param>
    /// <param name="byteCount">The value's bytes.</param>
    public void Written(int database, string key, long byteCount) => Note(new RedisKeyspaceChange(database, key, StoreChange.Written, byteCount));

    /// <summary>Notes that the key was removed.</summary>
    /// <param name="database">The database, 0 to 15.</param>
    /// <param name="key">The key.</param>
    public void Deleted(int database, string key) => Note(new RedisKeyspaceChange(database, key, StoreChange.Deleted, null));

    /// <summary>Notes that the whole database was emptied.</summary>
    /// <param name="database">The database, 0 to 15.</param>
    public void DatabaseDeleted(int database) => Note(new RedisKeyspaceChange(database, null, StoreChange.Deleted, null));

    /// <summary>Takes the changes noted since the last call, leaving the journal empty.</summary>
    /// <returns>The changes.</returns>
    public IReadOnlyList<RedisKeyspaceChange> Drain()
    {
        var drained = changes.ToArray();
        changes.Clear();
        return drained;
    }

    private void Note(RedisKeyspaceChange change)
    {
        var index = changes.FindIndex(existing => existing.Database == change.Database && existing.Key == change.Key);
        if (index < 0)
        {
            changes.Add(change);
        }
        else
        {
            changes[index] = change;
        }
    }
}
