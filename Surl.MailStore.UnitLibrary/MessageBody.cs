namespace Surl.MailStore;

/// <summary>
/// The bytes of one delivered message, shared by every copy of it (several recipients, IMAP
/// <c>COPY</c>) and counted once against the byte bound (ADR-0050, decisions 6 and 7).
/// </summary>
/// <param name="fileNumber">The message file number the bytes are persisted under.</param>
/// <param name="bytes">The message's bytes.</param>
internal sealed class MessageBody(ulong fileNumber, byte[] bytes)
{
    /// <summary>
    /// The message file number the bytes are persisted under, unique within the store.
    /// </summary>
    public ulong FileNumber { get; } = fileNumber;

    /// <summary>
    /// The message's bytes; a body read from the index is given its bytes once its message file
    /// has been read.
    /// </summary>
    public byte[] Bytes { get; set; } = bytes;

    /// <summary>
    /// How many stored messages refer to these bytes; at 0 they leave the store.
    /// </summary>
    public int ReferenceCount { get; set; }

    /// <summary>
    /// Whether the bytes are in their message file.
    /// </summary>
    public bool IsWritten { get; set; }
}
