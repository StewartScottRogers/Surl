namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The method IDs of the XML's <c>channel</c> class (class ID <see cref="ClassId"/>).
/// </summary>
public static class Amqp0ChannelMethodId
{
    /// <summary>The <c>channel</c> class: 20.</summary>
    public const ushort ClassId = 20;

    /// <summary><c>channel.open</c> (20.10), client to server.</summary>
    public const ushort Open = 10;

    /// <summary><c>channel.open-ok</c> (20.11), server to client.</summary>
    public const ushort OpenOk = 11;

    /// <summary><c>channel.flow</c> (20.20), client to server here: the broker never sends it.</summary>
    public const ushort Flow = 20;

    /// <summary><c>channel.flow-ok</c> (20.21), the broker's answer to <see cref="Flow"/>.</summary>
    public const ushort FlowOk = 21;

    /// <summary><c>channel.close</c> (20.40), either way.</summary>
    public const ushort Close = 40;

    /// <summary><c>channel.close-ok</c> (20.41), either way.</summary>
    public const ushort CloseOk = 41;
}
