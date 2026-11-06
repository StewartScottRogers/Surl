namespace Surl.Control;

/// <summary>
/// The snapshots the control endpoint keeps (ADR-0124 decision 7): at most <see cref="MaxSnapshots"/>, the oldest
/// dropped, under IDs <c>p1</c>, <c>p2</c>, ... never reused.
/// </summary>
internal sealed class ControlSnapshotShelf
{
    /// <summary>The most snapshots kept.</summary>
    public const int MaxSnapshots = 4;

    private readonly Lock gate = new();
    private readonly List<KeyValuePair<string, IControlSnapshot>> snapshots = [];
    private long lastNumber;

    /// <summary>Keeps <paramref name="snapshot"/>, dropping the oldest past the bound.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <returns>Its ID.</returns>
    public string Keep(IControlSnapshot snapshot)
    {
        lock (gate)
        {
            var snapshotId = $"p{++lastNumber}";
            snapshots.Add(new(snapshotId, snapshot));
            if (snapshots.Count > MaxSnapshots)
            {
                snapshots.RemoveAt(0);
            }

            return snapshotId;
        }
    }

    /// <summary>The snapshot kept under <paramref name="snapshotId"/>, or <see langword="null"/>.</summary>
    /// <param name="snapshotId">The ID.</param>
    /// <returns>The snapshot.</returns>
    public IControlSnapshot? Find(string snapshotId)
    {
        lock (gate)
        {
            return snapshots.FirstOrDefault(kept => kept.Key == snapshotId).Value;
        }
    }
}
