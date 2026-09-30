namespace Surl.MailStore;

/// <summary>
/// One message of a POP3 session's maildrop, fixed at login (ADR-0050, decision 4).
/// </summary>
/// <param name="Number">The message's POP3 number: 1 upward in UID order.</param>
/// <param name="Uid">The message's UID in the owner's <c>INBOX</c>.</param>
/// <param name="Size">The message's size in bytes.</param>
public readonly record struct MaildropMessage(int Number, uint Uid, long Size);
