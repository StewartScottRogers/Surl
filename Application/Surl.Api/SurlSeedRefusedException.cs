using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// A store refused a seed: mail for a recipient the
/// mail store does not know, a file at a path no client may write (a <c>.surl</c> path) or past
/// the size limit, a message on a topic the bus refuses, a queue that does not exist, a store
/// that is full, unreadable LDIF or zone text, or a store no listen URL serves (BL-810). A seed never
/// fails silently: every refusal is this exception, and a refused start-time seed (such as one of
/// <see cref="SurlServerOptions.RedisKeys"/>) refuses the start with it. An
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
