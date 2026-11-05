namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The method IDs of the XML's <c>connection</c> class (class ID <see cref="ClassId"/>).
/// </summary>
public static class Amqp0ConnectionMethodId
{
    /// <summary>The <c>connection</c> class: 10.</summary>
    public const ushort ClassId = 10;

    /// <summary><c>connection.start</c> (10.10), server to client.</summary>
    public const ushort Start = 10;

    /// <summary><c>connection.start-ok</c> (10.11), client to server.</summary>
    public const ushort StartOk = 11;

    /// <summary><c>connection.secure</c> (10.20), server to client.</summary>
    public const ushort Secure = 20;

    /// <summary><c>connection.secure-ok</c> (10.21), client to server.</summary>
    public const ushort SecureOk = 21;

    /// <summary><c>connection.tune</c> (10.30), server to client.</summary>
    public const ushort Tune = 30;

    /// <summary><c>connection.tune-ok</c> (10.31), client to server.</summary>
    public const ushort TuneOk = 31;

    /// <summary><c>connection.open</c> (10.40), client to server.</summary>
    public const ushort Open = 40;

    /// <summary><c>connection.open-ok</c> (10.41), server to client.</summary>
    public const ushort OpenOk = 41;

    /// <summary><c>connection.close</c> (10.50), either way.</summary>
    public const ushort Close = 50;

    /// <summary><c>connection.close-ok</c> (10.51), either way.</summary>
    public const ushort CloseOk = 51;
}
