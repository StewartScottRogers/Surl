namespace Surl.MailStore;

/// <summary>
/// One mailbox: its name, <c>UIDVALIDITY</c>, next UID and messages in ascending UID order.
/// </summary>
internal sealed class StoredMailbox(string name, uint uidValidity, uint nextUid)
{
    public string Name { get; } = name;

    public uint UidValidity { get; } = uidValidity;

    public uint NextUid { get; set; } = nextUid;

    public SortedList<uint, StoredMessage> Messages { get; set; } = [];

    /// <summary>
    /// Whether the mailbox can give <paramref name="count"/> more UIDs and still hold its next
    /// UID in a <see cref="uint"/>.
    /// </summary>
    public bool CanGiveUids(int count) => (long)NextUid + count <= uint.MaxValue;

    /// <summary>
    /// The UIDs of the messages flagged <c>\Deleted</c>, in ascending order.
    /// </summary>
    public IReadOnlyList<uint> DeletedUids() =>
        [.. Messages.Values.Where(message => message.Flags.HasFlag(MailFlags.Deleted)).Select(message => message.Uid)];

    public MailboxSnapshot Snapshot() =>
        new(Name, UidValidity, NextUid, [.. Messages.Values.Select(message => message.Summarize())]);
}
