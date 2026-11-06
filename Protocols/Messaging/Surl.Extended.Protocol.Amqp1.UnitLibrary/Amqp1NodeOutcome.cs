namespace Surl.Extended.Protocol.Amqp1;

/// <summary>Whether an attach's node was found, or why the attach is refused (ADR-0107 decision 4).</summary>
public enum Amqp1NodeOutcome
{
    /// <summary>The node exists, or was created, and the caller may use it.</summary>
    Found,

    /// <summary>The address names no node: <c>amqp:not-found</c>.</summary>
    NotFound,

    /// <summary>Topic access denies the caller: <c>amqp:unauthorized-access</c>.</summary>
    Unauthorized,

    /// <summary>The bus's bounds refuse the node: <c>amqp:resource-limit-exceeded</c>.</summary>
    ResourceLimitExceeded,
}
