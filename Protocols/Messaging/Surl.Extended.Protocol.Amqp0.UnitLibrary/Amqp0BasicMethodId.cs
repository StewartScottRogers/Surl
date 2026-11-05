namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The method IDs of the XML's <c>basic</c> class (class ID <see cref="ClassId"/>), with RabbitMQ's
/// <c>basic.nack</c> extension (ADR-0112 Context).
/// </summary>
public static class Amqp0BasicMethodId
{
    /// <summary>The <c>basic</c> class: 60, the only content class.</summary>
    public const ushort ClassId = 60;

    /// <summary><c>basic.qos</c> (60.10), client to server.</summary>
    public const ushort Qos = 10;

    /// <summary><c>basic.qos-ok</c> (60.11), server to client.</summary>
    public const ushort QosOk = 11;

    /// <summary><c>basic.consume</c> (60.20), client to server.</summary>
    public const ushort Consume = 20;

    /// <summary><c>basic.consume-ok</c> (60.21), server to client.</summary>
    public const ushort ConsumeOk = 21;

    /// <summary><c>basic.cancel</c> (60.30), client to server here.</summary>
    public const ushort Cancel = 30;

    /// <summary><c>basic.cancel-ok</c> (60.31), server to client here.</summary>
    public const ushort CancelOk = 31;

    /// <summary><c>basic.publish</c> (60.40), client to server, followed by content.</summary>
    public const ushort Publish = 40;

    /// <summary><c>basic.return</c> (60.50), server to client, followed by content.</summary>
    public const ushort Return = 50;

    /// <summary><c>basic.deliver</c> (60.60), server to client, followed by content.</summary>
    public const ushort Deliver = 60;

    /// <summary><c>basic.get</c> (60.70), client to server.</summary>
    public const ushort Get = 70;

    /// <summary><c>basic.get-ok</c> (60.71), server to client, followed by content.</summary>
    public const ushort GetOk = 71;

    /// <summary><c>basic.get-empty</c> (60.72), server to client.</summary>
    public const ushort GetEmpty = 72;

    /// <summary><c>basic.ack</c> (60.80), client to server here.</summary>
    public const ushort Ack = 80;

    /// <summary><c>basic.reject</c> (60.90), client to server.</summary>
    public const ushort Reject = 90;

    /// <summary><c>basic.recover-async</c> (60.100), client to server, answered by nothing.</summary>
    public const ushort RecoverAsync = 100;

    /// <summary><c>basic.recover</c> (60.110), client to server.</summary>
    public const ushort Recover = 110;

    /// <summary><c>basic.recover-ok</c> (60.111), server to client.</summary>
    public const ushort RecoverOk = 111;

    /// <summary><c>basic.nack</c> (60.120, RabbitMQ's extension), client to server here.</summary>
    public const ushort Nack = 120;
}
