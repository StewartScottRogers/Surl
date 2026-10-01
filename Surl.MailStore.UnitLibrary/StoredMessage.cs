namespace Surl.MailStore;

/// <summary>
/// One message in one mailbox.
/// </summary>
internal sealed class StoredMessage(uint uid, MessageBody body, DateTimeOffset internalDate, MailFlags flags)
{
    public uint Uid { get; } = uid;

    public MessageBody Body { get; } = body;

    public DateTimeOffset InternalDate { get; } = internalDate;

    public MailFlags Flags { get; set; } = flags;

    public MailMessageSummary Summarize() => new(Uid, Flags, InternalDate, Body.Length);
}
