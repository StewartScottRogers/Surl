namespace Surl.Protocol.Abstractions;

/// <summary>
/// Where in an exchange a <see cref="FaultPoint"/> asks the fault plan (ADR-0124, decision 4).
/// </summary>
public enum FaultStage
{
    /// <summary>
    /// A connection was accepted, or a datagram flow's first datagram arrived, before any byte (TLS included).
    /// </summary>
    Accept,

    /// <summary>
    /// A login is being judged.
    /// </summary>
    Login,

    /// <summary>
    /// A request, command, packet or operation a server consults the plan on.
    /// </summary>
    Command,

    /// <summary>
    /// A store is about to write.
    /// </summary>
    StoreWrite,
}
