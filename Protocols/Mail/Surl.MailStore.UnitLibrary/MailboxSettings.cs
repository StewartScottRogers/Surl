namespace Surl.MailStore;

/// <summary>
/// The <c>quota=</c> and <c>folders=</c> keys of one <c>--mailbox</c> (ADR-0102, decision 1.4).
/// </summary>
/// <param name="Address">The mailbox's address, one of the configuration's mailbox addresses.</param>
/// <param name="QuotaBytes">The most bytes the owner's own mailboxes may hold, every copy at its
/// full size, above 0; or <see langword="null"/> for no quota.</param>
/// <param name="Folders">The mailboxes created for the owner at start when missing; <c>INBOX</c>
/// in any case is skipped.</param>
public sealed record MailboxSettings(string Address, long? QuotaBytes, IReadOnlyList<string> Folders);
