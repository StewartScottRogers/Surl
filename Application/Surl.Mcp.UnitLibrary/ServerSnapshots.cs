using Surl.Api;

namespace Surl.Mcp;

/// <summary>
/// The snapshots one running server's <c>snapshot_server</c> calls took (ADR-0124 decision 8): kept in memory under
/// IDs <c>p1</c>, <c>p2</c> ... (never reused for that server), at most <see cref="MaxSnapshots"/>, the oldest dropped
/// when a newer one is kept. A snapshot is referred to by its ID and never returned whole. Thread-safe.
/// </summary>
public sealed class ServerSnapshots
{
    /// <summary>The most snapshots kept per server.</summary>
    public const int MaxSnapshots = 4;

    private readonly Lock gate = new();
    private readonly List<(string SnapshotId, SurlSnapshot Snapshot)> kept = [];
    private long lastSnapshotNumber;

    /// <summary>Keeps <paramref name="snapshot"/> under the next ID, dropping the oldest kept when there are already <see cref="MaxSnapshots"/>.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <returns>Its ID.</returns>
    public string Keep(SurlSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (gate)
        {
            var snapshotId = $"p{++lastSnapshotNumber}";
            if (kept.Count == MaxSnapshots)
            {
                kept.RemoveAt(0);
            }

            kept.Add((snapshotId, snapshot));
            return snapshotId;
        }
    }

    /// <summary>Finds the kept snapshot <paramref name="snapshotId"/> names.</summary>
    /// <param name="snapshotId">The snapshot's ID.</param>
    /// <returns>The snapshot, or <see langword="null"/> when none is kept under that ID (never taken, or dropped as the oldest).</returns>
    public SurlSnapshot? Find(string snapshotId)
    {
        ArgumentNullException.ThrowIfNull(snapshotId);
        lock (gate)
        {
            return kept.Find(entry => entry.SnapshotId == snapshotId).Snapshot;
        }
    }
}
