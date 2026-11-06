namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// A BINARY_EVENT or BINARY_ACK whose attachments are still arriving, one binary Engine.IO
/// <c>message</c> packet each (SIO "Packet encoding"; ADR-0120, decisions 6 and 7). Its text part and
/// every attachment together stay within <c>--max-message</c>.
/// </summary>
/// <param name="packet">The text part, read.</param>
/// <param name="textBytes">The text part's size.</param>
/// <param name="maxMessageBytes">The most bytes the text part and attachments may hold together; 0 means no limit.</param>
internal sealed class SocketIoPendingBinaryPacket(SocketIoPacket packet, long textBytes, long maxMessageBytes)
{
    private readonly List<byte[]> attachments = [];
    private long totalBytes = textBytes;

    /// <summary>
    /// The text part.
    /// </summary>
    public SocketIoPacket Packet { get; } = packet;

    /// <summary>
    /// The attachments received so far, in order.
    /// </summary>
    public IReadOnlyList<byte[]> Attachments => attachments;

    /// <summary>
    /// Whether every attachment the packet counts has arrived.
    /// </summary>
    public bool IsComplete => attachments.Count == Packet.AttachmentCount;

    /// <summary>
    /// Takes the next attachment, unless it carries the packet past the limit.
    /// </summary>
    /// <returns><see langword="false"/> when the packet would pass <c>--max-message</c>.</returns>
    public bool TryAdd(byte[] attachment)
    {
        totalBytes += attachment.Length;
        attachments.Add(attachment);

        return maxMessageBytes == 0 || totalBytes <= maxMessageBytes;
    }
}
