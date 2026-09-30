namespace Surl.MailStore;

/// <summary>
/// The bytes of one delivered message, shared by every copy of it (several recipients, IMAP
/// <c>COPY</c>) and counted once against the byte bound (ADR-0050, decisions 6 and 7).
/// </summary>
/// <param name="fileNumber">The message file number the bytes are persisted under.</param>
/// <param name="length">How many bytes the message holds.</param>
/// <param name="bytes">The message's bytes when they are held in memory; <see langword="null"/>
/// when they are in their message file.</param>
internal sealed class MessageBody(ulong fileNumber, long length, byte[]? bytes)
{
    /// <summary>
    /// The message file number the bytes are persisted under, unique within the store.
    /// </summary>
    public ulong FileNumber { get; } = fileNumber;

    /// <summary>
    /// How many bytes the message holds.
    /// </summary>
    public long Length { get; } = length;

    /// <summary>
    /// The message's bytes held in memory: by a store without files, or by one with files for a
    /// message given whole until the next save writes its file. <see langword="null"/> when they
    /// are read from their message file on fetch.
    /// </summary>
    public byte[]? Bytes { get; set; } = bytes;

    /// <summary>
    /// How many stored messages, and POP3 maildrop locks, refer to these bytes; at 0 they
    /// leave the store.
    /// </summary>
    public int ReferenceCount { get; set; }
}
