namespace Surl.MailStore;

/// <summary>
/// A mailbox as it was at one moment: what IMAP's <c>SELECT</c>, <c>EXAMINE</c> and
/// <c>STATUS</c> report.
/// </summary>
/// <param name="Name">The mailbox's name (<c>INBOX</c> in capitals).</param>
/// <param name="UidValidity">The mailbox's <c>UIDVALIDITY</c>.</param>
/// <param name="NextUid">The UID the next message stored in the mailbox gets.</param>
/// <param name="Messages">The mailbox's messages in ascending UID order.</param>
public sealed record MailboxSnapshot(string Name, uint UidValidity, uint NextUid, IReadOnlyList<MailMessageSummary> Messages);
