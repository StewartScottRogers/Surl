using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// A store of a composed instance refused a reset, snapshot or restore (ADR-0124, decision 3): a
/// store on disk (<c>--directory</c>), a snapshot past <see cref="ComposedStores.MaxSnapshotBytes"/>,
/// or the store's own refusal. It names the store, so a whole-instance operation that stops at its
/// first refusal says where it stopped. An <see cref="InvalidOperationException"/>, as the store's
/// own refusal is, so a host answers both alike (the control endpoint with <c>409</c>).
/// </summary>
public sealed class ComposedStoreException : InvalidOperationException
{
    /// <summary>Creates the exception with a message of its own.</summary>
    /// <param name="store">The store that refused.</param>
    /// <param name="message">Why, as a sentence.</param>
    internal ComposedStoreException(StoreKind store, string message)
        : base($"{store}: {message}")
    {
        Store = store;
    }

    /// <summary>Creates the exception for a store's own refusal.</summary>
    /// <param name="store">The store that refused.</param>
    /// <param name="done">What it was not, such as <c>reset</c>.</param>
    /// <param name="refusal">The store's refusal.</param>
    internal ComposedStoreException(StoreKind store, string done, InvalidOperationException refusal)
        : base($"{store}: the store could not be {done}. {refusal.Message}", refusal)
    {
        Store = store;
    }

    /// <summary>The store that refused.</summary>
    public StoreKind Store { get; }
}
