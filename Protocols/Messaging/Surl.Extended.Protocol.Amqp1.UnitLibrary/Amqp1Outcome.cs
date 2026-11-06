namespace Surl.Extended.Protocol.Amqp1;

/// <summary>Which delivery state of Part 3 section 3.4 a <c>state</c> field holds.</summary>
public enum Amqp1Outcome
{
    /// <summary>No state, or a value that is no delivery state of Part 3 section 3.4.</summary>
    None,

    /// <summary><c>received</c>: how far a delivery got, which is no outcome.</summary>
    Received,

    /// <summary><c>accepted</c>: the message was processed.</summary>
    Accepted,

    /// <summary><c>rejected</c>: the message is refused and must not be delivered again.</summary>
    Rejected,

    /// <summary><c>released</c>: the message was not processed and may be delivered again.</summary>
    Released,

    /// <summary><c>modified</c>: the message was not processed and may be delivered again, changed.</summary>
    Modified,
}
