namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The method IDs of the XML's <c>exchange</c> class (class ID <see cref="ClassId"/>), and the two
/// RabbitMQ extension methods the broker refuses with 540 (ADR-0112 decision 7).
/// </summary>
public static class Amqp0ExchangeMethodId
{
    /// <summary>The <c>exchange</c> class: 40.</summary>
    public const ushort ClassId = 40;

    /// <summary><c>exchange.declare</c> (40.10), client to server.</summary>
    public const ushort Declare = 10;

    /// <summary><c>exchange.declare-ok</c> (40.11), server to client.</summary>
    public const ushort DeclareOk = 11;

    /// <summary><c>exchange.delete</c> (40.20), client to server.</summary>
    public const ushort Delete = 20;

    /// <summary><c>exchange.delete-ok</c> (40.21), server to client.</summary>
    public const ushort DeleteOk = 21;
}
