using Surl.MailStore;

namespace Surl.Protocol.Imap;

/// <summary>
/// What a <c>FETCH</c> answers one message from.
/// </summary>
/// <param name="Number">The message's number in the session's view.</param>
/// <param name="Uid">The message's UID.</param>
/// <param name="Flags">The message's flags, <c>\Seen</c> included when this fetch set it.</param>
/// <param name="IsSeenSet">Whether this fetch set <c>\Seen</c>, so <c>FLAGS</c> is answered unasked.</param>
/// <param name="InternalDate">The message's internal date.</param>
/// <param name="Size">The message's size in bytes.</param>
/// <param name="Message">The message read from its bytes.</param>
internal sealed record ImapFetchedMessage(int Number, uint Uid, MailFlags Flags, bool IsSeenSet, DateTimeOffset InternalDate, long Size, ImapBodyPart Message);
