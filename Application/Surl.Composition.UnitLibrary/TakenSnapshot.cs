using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// One store's part of a <see cref="ComposedSnapshot"/>: which store, the bytes it keeps alive, and
/// how to put it back into the store that took it.
/// </summary>
/// <param name="Store">The store.</param>
/// <param name="ByteCount">The bytes the store's snapshot holds, as the store counts them.</param>
/// <param name="RestoreAsync">Replaces the store's contents with the snapshot's.</param>
internal sealed record TakenSnapshot(StoreKind Store, long ByteCount, Func<CancellationToken, Task> RestoreAsync);
