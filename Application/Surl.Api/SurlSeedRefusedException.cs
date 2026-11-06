using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// A store refused a seed a peer would also have been refused (BL-810): mail for a recipient the
/// mail store does not know, a file at a path no client may write (a <c>.surl</c> path) or past
/// the size limit, a message on a topic the bus refuses, a queue that does not exist, or a store
/// that is full. A seed never fails silently: every refusal is this exception. An
/// <see cref="InvalidOperationException"/>, as a store's refusal of a reset is.
/// </summary>
public sealed class SurlSeedRefusedException : InvalidOperationException
{
    internal SurlSeedRefusedException(StoreKind store, string refusal, string what)
        : base($"{store}: {what} was refused: {refusal}.")
    {
        Store = store;
        Refusal = refusal;
    }

    /// <summary>The store that refused.</summary>
    public StoreKind Store { get; }

    /// <summary>
    /// The store's own name for the refusal, such as <c>NoSuchAccount</c>, <c>NotPermitted</c>,
    /// <c>TooLarge</c>, <c>UnknownQueue</c> or <c>StoreFull</c>.
    /// </summary>
    public string Refusal { get; }
}
