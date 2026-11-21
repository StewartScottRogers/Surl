namespace Surl.Api;

/// <summary>One mailbox of an account, as <see cref="SurlServerMail.ListMailboxesAsync"/> reads it.</summary>
/// <param name="Name">The mailbox's name, such as <c>INBOX</c>.</param>
/// <param name="MessageCount">How many messages it holds.</param>
/// <param name="UnseenCount">How many of them lack <c>\Seen</c>.</param>
/// <param name="UidValidity">The mailbox's <c>UIDVALIDITY</c>.</param>
/// <param name="Use">The mailbox's RFC 6154 use in its canonical spelling, such as <c>\Sent</c>; <see langword="null"/> for none.</param>
public sealed record SurlMailboxInfo(string Name, int MessageCount, int UnseenCount, uint UidValidity, string? Use = null);
