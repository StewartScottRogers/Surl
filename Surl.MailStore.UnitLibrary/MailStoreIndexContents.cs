namespace Surl.MailStore;

/// <summary>
/// What a mail store index holds once decoded: the store's owners, mailboxes and messages,
/// each distinct message's body still without its bytes, and the counts the store's bounds
/// are checked against.
/// </summary>
/// <param name="nextFileNumber">The next message file number to give.</param>
/// <param name="lastUidValidity">The last <c>UIDVALIDITY</c> given.</param>
internal sealed class MailStoreIndexContents(ulong nextFileNumber, uint lastUidValidity)
{
    public ulong NextFileNumber { get; } = nextFileNumber;

    public uint LastUidValidity { get; } = lastUidValidity;

    public List<OwnerMailboxes> Owners { get; } = [];

    /// <summary>
    /// Each distinct message's body by its file number, with the size the index gives it.
    /// </summary>
    public Dictionary<ulong, (MessageBody Body, long Size)> Bodies { get; } = [];

    public int MessageCount { get; set; }

    public long TotalMessageBytes { get; set; }

    /// <summary>
    /// The mailboxes other than the owners' <c>INBOX</c>es.
    /// </summary>
    public int MailboxCount { get; set; }
}
