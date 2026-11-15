using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// One store's entry in a <see cref="ComposedStores"/> registration list (ADR-0124, decision 3): how
/// to reset it and how to take its snapshot, which carries its own restore. A whole-instance reset,
/// snapshot and restore walk the list in order, so a new store joins all three by one entry.
/// </summary>
/// <param name="Store">The store this entry controls.</param>
/// <param name="ResetAsync">Returns the store to its configured state; throws an
/// <see cref="InvalidOperationException"/> to refuse, such as a store on disk.</param>
/// <param name="TakeSnapshotAsync">Takes the store's snapshot, given the bytes still free under
/// <see cref="ComposedStores.MaxSnapshotBytes"/>; throws an <see cref="InvalidOperationException"/> to
/// refuse.</param>
internal sealed record ComposedStoreRegistration(
    StoreKind Store,
    Func<CancellationToken, Task> ResetAsync,
    Func<long, CancellationToken, Task<TakenSnapshot>> TakeSnapshotAsync);
