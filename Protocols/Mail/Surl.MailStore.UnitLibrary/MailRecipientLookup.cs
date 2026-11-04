namespace Surl.MailStore;

/// <summary>
/// What <see cref="MailboxStore.LookUpRecipient"/> found for an SMTP <c>RCPT TO</c> path
/// (ADR-0050, decision 5).
/// </summary>
public enum MailRecipientLookup
{
    /// <summary>The path names an owner; mail for it is delivered to that owner's <c>INBOX</c>.</summary>
    Deliverable,

    /// <summary>The path is valid but names no account. The server accepts it exactly as a
    /// deliverable one and discards its copy, so a peer cannot learn which accounts exist.</summary>
    NoSuchAccount,

    /// <summary>The path is not a valid RFC 5321 forward path; the server refuses it at
    /// <c>RCPT</c>.</summary>
    InvalidAddress,
}
