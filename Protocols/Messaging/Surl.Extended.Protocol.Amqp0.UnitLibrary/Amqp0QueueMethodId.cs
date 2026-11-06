namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The method IDs of the XML's <c>queue</c> class (class ID <see cref="ClassId"/>).
/// </summary>
public static class Amqp0QueueMethodId
{
    /// <summary>The <c>queue</c> class: 50.</summary>
    public const ushort ClassId = 50;

    /// <summary><c>queue.declare</c> (50.10), client to server.</summary>
    public const ushort Declare = 10;

    /// <summary><c>queue.declare-ok</c> (50.11), server to client.</summary>
    public const ushort DeclareOk = 11;

    /// <summary><c>queue.bind</c> (50.20), client to server.</summary>
    public const ushort Bind = 20;

    /// <summary><c>queue.bind-ok</c> (50.21), server to client.</summary>
    public const ushort BindOk = 21;

    /// <summary><c>queue.purge</c> (50.30), client to server.</summary>
    public const ushort Purge = 30;

    /// <summary><c>queue.purge-ok</c> (50.31), server to client.</summary>
    public const ushort PurgeOk = 31;

    /// <summary><c>queue.delete</c> (50.40), client to server.</summary>
    public const ushort Delete = 40;

    /// <summary><c>queue.delete-ok</c> (50.41), server to client.</summary>
    public const ushort DeleteOk = 41;

    /// <summary><c>queue.unbind</c> (50.50), client to server.</summary>
    public const ushort Unbind = 50;

    /// <summary><c>queue.unbind-ok</c> (50.51), server to client.</summary>
    public const ushort UnbindOk = 51;
}
