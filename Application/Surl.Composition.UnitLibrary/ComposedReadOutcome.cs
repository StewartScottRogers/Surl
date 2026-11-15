namespace Surl.Composition;

/// <summary>How a read-back of a store ended (ADR-0164, decisions 1 and 2).</summary>
public enum ComposedReadOutcome
{
    /// <summary>The item was found and is in the result's value.</summary>
    Found,

    /// <summary>The owner, mailbox or message is absent.</summary>
    NotFound,

    /// <summary>The item is larger than <see cref="ComposedStores.MaxReadBytes"/>; it is refused, never cut short.</summary>
    TooLarge,

    /// <summary>The arguments are outside what the read accepts, such as a limit outside 1 to 1000.</summary>
    Refused,
}
