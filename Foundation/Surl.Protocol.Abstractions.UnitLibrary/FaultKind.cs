namespace Surl.Protocol.Abstractions;

/// <summary>
/// The five ways a <see cref="FaultRule"/> makes a server misbehave on purpose (ADR-0124, decision 4).
/// A new kind needs an ADR.
/// </summary>
public enum FaultKind
{
    /// <summary>
    /// Wait before going on, at <see cref="FaultStage.Accept"/> or <see cref="FaultStage.Command"/>.
    /// </summary>
    Delay,

    /// <summary>
    /// Abort the connection, or stop answering a datagram flow.
    /// </summary>
    Drop,

    /// <summary>
    /// Refuse a login at <see cref="FaultStage.Login"/> as a wrong password is refused.
    /// </summary>
    RefuseLogin,

    /// <summary>
    /// Answer a command with the rule's chosen error, in the protocol's own error shape.
    /// </summary>
    Error,

    /// <summary>
    /// Refuse a store write at <see cref="FaultStage.StoreWrite"/> as a full quota is refused.
    /// </summary>
    OutOfSpace,
}
