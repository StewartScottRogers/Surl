using Surl.Control;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// An opaque, in-memory snapshot of one composed instance's stores (ADR-0124, decision 3): one
/// snapshot per store it holds, taken by <see cref="ComposedStores.TakeSnapshotAsync(CancellationToken)"/>
/// or <see cref="ComposedStores.TakeSnapshotAsync(StoreKind, CancellationToken)"/> and given back by
/// <see cref="ComposedStores.RestoreAsync"/> to the instance that took it, and to no other, any
/// number of times.
/// </summary>
/// <remarks>
/// Public because more than one host calls it, but not a supported programming interface: it may
/// change in any release (ADR-0099, decision 1).
/// </remarks>
public sealed class ComposedSnapshot : IControlSnapshot
{
    internal ComposedSnapshot(ComposedStores owner, IReadOnlyList<TakenSnapshot> taken)
    {
        Owner = owner;
        Taken = taken;
    }

    /// <summary>The stores the snapshot holds, in <see cref="ComposedStores.StoreOrder"/>.</summary>
    public IReadOnlyList<StoreKind> Stores => [.. Taken.Select(snapshot => snapshot.Store)];

    /// <summary>
    /// The bytes the snapshot holds, every store's together, as each store counts them; never more
    /// than <see cref="ComposedStores.MaxSnapshotBytes"/>.
    /// </summary>
    public long ByteCount => Taken.Sum(snapshot => snapshot.ByteCount);

    /// <summary>The instance whose stores were copied, the only one that restores it.</summary>
    internal ComposedStores Owner { get; }

    /// <summary>Each store's snapshot, in the order they are restored.</summary>
    internal IReadOnlyList<TakenSnapshot> Taken { get; }
}
