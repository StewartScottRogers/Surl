namespace Surl.Extended.Protocol.Amqp1;

/// <summary>How <see cref="IAmqp1NodeDirectory"/> answered an attach.</summary>
/// <param name="Outcome">Whether the node was found and may be used, or why not.</param>
/// <param name="CanonicalAddress">The node's canonical address when found; otherwise <see langword="null"/>.</param>
public sealed record Amqp1NodeResolution(Amqp1NodeOutcome Outcome, string? CanonicalAddress)
{
    /// <summary>No such node (ADR-0107 decision 4: <c>amqp:not-found</c>).</summary>
    public static Amqp1NodeResolution NotFound { get; } = new(Amqp1NodeOutcome.NotFound, null);

    /// <summary>Topic access denies the caller (<c>amqp:unauthorized-access</c>).</summary>
    public static Amqp1NodeResolution Unauthorized { get; } = new(Amqp1NodeOutcome.Unauthorized, null);

    /// <summary>The bus refuses the queue or subscription (<c>amqp:resource-limit-exceeded</c>).</summary>
    public static Amqp1NodeResolution ResourceLimitExceeded { get; } = new(Amqp1NodeOutcome.ResourceLimitExceeded, null);

    /// <summary>The node was found, or created, and may be used.</summary>
    /// <param name="canonicalAddress">Its canonical address, <c>/topic/&lt;t&gt;</c> or <c>/queue/&lt;q&gt;</c>.</param>
    /// <returns>The resolution.</returns>
    public static Amqp1NodeResolution Found(string canonicalAddress)
    {
        ArgumentNullException.ThrowIfNull(canonicalAddress);

        return new(Amqp1NodeOutcome.Found, canonicalAddress);
    }
}
