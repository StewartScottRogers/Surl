namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// The Socket.IO v5 packet types, numbered as their type digit numbers them on the wire (SIO
/// "Exchange protocol"; ADR-0120, decision 5).
/// </summary>
public enum SocketIoPacketType
{
    /// <summary><c>0</c> CONNECT: asks to connect a namespace, or answers that it is connected.</summary>
    Connect = 0,

    /// <summary><c>1</c> DISCONNECT: leaves a namespace.</summary>
    Disconnect = 1,

    /// <summary><c>2</c> EVENT: an event and its arguments, a JSON array.</summary>
    Event = 2,

    /// <summary><c>3</c> ACK: acknowledges an event, carrying its ack id.</summary>
    Ack = 3,

    /// <summary><c>4</c> CONNECT_ERROR: refuses a CONNECT; only the server sends it.</summary>
    ConnectError = 4,

    /// <summary><c>5</c> BINARY_EVENT: an event whose arguments hold binary attachments.</summary>
    BinaryEvent = 5,

    /// <summary><c>6</c> BINARY_ACK: an acknowledgement whose arguments hold binary attachments.</summary>
    BinaryAck = 6,
}
