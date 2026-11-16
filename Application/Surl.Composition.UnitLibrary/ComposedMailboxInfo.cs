namespace Surl.Composition;

/// <summary>One mailbox of an account, as a test reads it back.</summary>
/// <param name="Name">The mailbox's name (<c>INBOX</c> in capitals).</param>
/// <param name="MessageCount">How many messages it holds.</param>
/// <param name="UnseenCount">How many of them lack <c>\Seen</c>.</param>
/// <param name="UidValidity">The mailbox's <c>UIDVALIDITY</c>.</param>
public sealed record ComposedMailboxInfo(string Name, int MessageCount, int UnseenCount, uint UidValidity);
