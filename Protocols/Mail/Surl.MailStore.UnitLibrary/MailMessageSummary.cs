namespace Surl.MailStore;

/// <summary>
/// One stored message as a mailbox snapshot shows it, without its bytes.
/// </summary>
/// <param name="Uid">The message's UID in its mailbox.</param>
/// <param name="Flags">The message's flags.</param>
/// <param name="InternalDate">When the message was delivered, or the date an IMAP <c>APPEND</c>
/// gave, with its offset.</param>
/// <param name="Size">The message's size in bytes.</param>
public readonly record struct MailMessageSummary(uint Uid, MailFlags Flags, DateTimeOffset InternalDate, long Size);
