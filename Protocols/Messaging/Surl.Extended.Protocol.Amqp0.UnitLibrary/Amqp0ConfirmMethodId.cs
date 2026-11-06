namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The method IDs of RabbitMQ's <c>confirm</c> class extension (class ID <see cref="ClassId"/>;
/// ADR-0112 decision 6).
/// </summary>
public static class Amqp0ConfirmMethodId
{
    /// <summary>The <c>confirm</c> class: 85.</summary>
    public const ushort ClassId = 85;

    /// <summary><c>confirm.select</c> (85.10), client to server, with <c>nowait</c>.</summary>
    public const ushort Select = 10;

    /// <summary><c>confirm.select-ok</c> (85.11), server to client.</summary>
    public const ushort SelectOk = 11;
}
