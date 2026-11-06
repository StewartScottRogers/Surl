namespace Surl.MailStore;

/// <summary>
/// One message in a <see cref="MailboxCopy"/>: its UID, bytes, internal date and flags.
/// </summary>
internal sealed record MessageCopy(uint Uid, MessageBody Body, DateTimeOffset InternalDate, MailFlags Flags);
