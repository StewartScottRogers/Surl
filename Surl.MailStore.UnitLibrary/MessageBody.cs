namespace Surl.MailStore;

/// <summary>
/// The bytes of one delivered message, shared by every copy of it (several recipients, IMAP
/// <c>COPY</c>) and counted once against the byte bound (ADR-0050, decisions 6 and 7).
/// </summary>
internal sealed class MessageBody(byte[] bytes)
{
    public byte[] Bytes { get; } = bytes;

    /// <summary>
    /// How many stored messages refer to these bytes; at 0 they leave the store.
    /// </summary>
    public int ReferenceCount { get; set; }
}
