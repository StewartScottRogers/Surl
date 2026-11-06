namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// What <see cref="Amqp0ProtocolHeader.ReadAsync"/> found in the client's first 8 bytes.
/// </summary>
public enum Amqp0ProtocolHeaderOutcome
{
    /// <summary>The header was <c>AMQP 0 0 9 1</c>; the broker goes on to <c>connection.start</c>.</summary>
    Accepted,

    /// <summary>Another header: it was answered with the supported one and writes were completed.</summary>
    Refused,

    /// <summary>The client closed before sending 8 bytes; nothing was written.</summary>
    ConnectionClosedEarly,
}
