namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// One Socket.IO v5 packet, as the default parser encodes it:
/// <c>&lt;type&gt;[&lt;attachments&gt;-][&lt;namespace&gt;,][&lt;ack id&gt;][&lt;JSON payload&gt;]</c> (SIO "Packet
/// encoding"; ADR-0120, decisions 5 and 6).
/// </summary>
/// <param name="Type">The packet type.</param>
/// <param name="Namespace">The namespace, <c>/</c> for the main one, which the encoding leaves out.</param>
/// <param name="AttachmentCount">
/// How many binary attachments follow: at least 1 for a BINARY_EVENT or BINARY_ACK, 0 for every other type.
/// </param>
/// <param name="AckId">The ack id, or <see langword="null"/> when the packet carries none.</param>
/// <param name="Payload">
/// The JSON payload byte for byte as received or to be sent, binary values as
/// <c>{"_placeholder":true,"num":N}</c>; empty when the packet has none.
/// </param>
public sealed record SocketIoPacket(
    SocketIoPacketType Type,
    string Namespace,
    int AttachmentCount,
    long? AckId,
    ReadOnlyMemory<byte> Payload)
{
    /// <summary>The main namespace.</summary>
    public const string MainNamespace = "/";

    /// <summary>Says whether packets of <paramref name="type"/> carry an attachment count.</summary>
    /// <param name="type">The packet type.</param>
    /// <returns><see langword="true"/> for BINARY_EVENT and BINARY_ACK.</returns>
    public static bool CarriesAttachments(SocketIoPacketType type) =>
        type is SocketIoPacketType.BinaryEvent or SocketIoPacketType.BinaryAck;

    /// <summary>Says whether packets of <paramref name="type"/> may carry an ack id.</summary>
    /// <param name="type">The packet type.</param>
    /// <returns><see langword="true"/> for EVENT, ACK, BINARY_EVENT and BINARY_ACK.</returns>
    public static bool MayCarryAckId(SocketIoPacketType type) =>
        type is SocketIoPacketType.Event or SocketIoPacketType.Ack
            or SocketIoPacketType.BinaryEvent or SocketIoPacketType.BinaryAck;

    /// <summary>Says whether packets of <paramref name="type"/> must carry an ack id.</summary>
    /// <param name="type">The packet type.</param>
    /// <returns><see langword="true"/> for ACK and BINARY_ACK.</returns>
    public static bool RequiresAckId(SocketIoPacketType type) =>
        type is SocketIoPacketType.Ack or SocketIoPacketType.BinaryAck;
}
