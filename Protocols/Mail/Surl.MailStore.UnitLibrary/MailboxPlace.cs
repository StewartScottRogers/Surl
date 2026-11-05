namespace Surl.MailStore;

/// <summary>
/// A mailbox a view reaches and the owner that holds it: the view's own owner, or the reserved
/// shared-folder owner (ADR-0102, decision 1.6).
/// </summary>
/// <param name="Owner">The owner holding the mailbox, whose quota a message stored in it counts against.</param>
/// <param name="Mailbox">The mailbox.</param>
internal readonly record struct MailboxPlace(OwnerMailboxes Owner, StoredMailbox Mailbox);
