namespace Surl.MailStore;

/// <summary>
/// What a <see cref="MailboxStore"/> operation did. Every refusal a peer can cause is one of
/// these rather than an exception (ADR-0050, decision 3); each server answers it in its own
/// words.
/// </summary>
public enum MailStoreOutcome
{
    /// <summary>The operation was carried out.</summary>
    Succeeded,

    /// <summary>The named mailbox does not exist in the view, or the view is empty.</summary>
    MailboxMissing,

    /// <summary>The mailbox holds no message with the given UID.</summary>
    MessageMissing,

    /// <summary>A mailbox of the new name already exists (<c>INBOX</c> always does).</summary>
    AlreadyExists,

    /// <summary>The mailbox name is empty, longer than
    /// <see cref="MailboxStore.MaxMailboxNameBytes"/> UTF-8 bytes, holds a control character, or
    /// is not valid UTF-16.</summary>
    InvalidName,

    /// <summary><c>INBOX</c> cannot be deleted.</summary>
    InboxCannotBeDeleted,

    /// <summary>Storing the messages would pass <see cref="MailboxStore.MaxMessages"/>,
    /// <see cref="MailboxStore.MaxTotalMessageBytes"/> or the last UID a mailbox can give, or a
    /// new mailbox would need a <c>UIDVALIDITY</c> past the last a <see cref="uint"/> holds;
    /// nothing was stored.</summary>
    StoreFull,

    /// <summary>Creating the mailbox would pass <see cref="MailboxStore.MaxMailboxes"/>.</summary>
    TooManyMailboxes,

    /// <summary>The message is larger than <see cref="MailboxStore.MaxMessageBytes"/>; nothing
    /// was stored.</summary>
    MessageTooLarge,

    /// <summary>Another POP3 session holds the owner's maildrop lock.</summary>
    MaildropLocked,

    /// <summary>The message's pending file could not be created, written or renamed into place
    /// (ADR-0050, decision 7); <see cref="PendingMessage.StorageFailure"/> holds the exception's
    /// message, and nothing was stored.</summary>
    StorageFailed,

    /// <summary>Storing the messages would pass the quota of an owner they go to (ADR-0102,
    /// decision 1.4): IMAP answers <c>NO [OVERQUOTA]</c>, SMTP <c>452 4.2.2</c>. Nothing was
    /// stored, for any recipient.</summary>
    QuotaExceeded,

    /// <summary>The operation would delete or rename a shared folder or <c>Shared</c>, or create
    /// or rename a mailbox to <c>Shared</c> or a name under <c>Shared/</c> while shared folders
    /// are configured (ADR-0102, decision 1.6): IMAP answers <c>NO [NOPERM]</c>.</summary>
    SharedFolderProtected,
}
