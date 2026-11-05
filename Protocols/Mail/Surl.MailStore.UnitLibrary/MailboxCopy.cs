namespace Surl.MailStore;

/// <summary>
/// One mailbox in a <see cref="MailStoreSnapshot"/>: its name, next UID and messages.
/// </summary>
internal sealed record MailboxCopy(string Name, uint NextUid, IReadOnlyList<MessageCopy> Messages);
