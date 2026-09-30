namespace Surl.Protocol.Ssh;

/// <summary>
/// Thrown where the server refuses what the client sent: the exchange ends with an
/// <c>SSH_MSG_DISCONNECT</c> carrying <see cref="Reason"/> and <see cref="Description"/>,
/// after <see cref="Exception.Message"/> is noted in the exchange log (ADR-0051, decision 9).
/// </summary>
internal sealed class SshDisconnectRequiredException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="reason">The reason code the <c>DISCONNECT</c> carries.</param>
    /// <param name="description">The fixed description the <c>DISCONNECT</c> carries; it names nothing the peer did not send.</param>
    /// <param name="note">What the operator is told about the refusal.</param>
    public SshDisconnectRequiredException(SshDisconnectReason reason, string description, string note)
        : base(note)
    {
        Reason = reason;
        Description = description;
    }

    /// <summary>
    /// The reason code the <c>DISCONNECT</c> carries.
    /// </summary>
    public SshDisconnectReason Reason { get; }

    /// <summary>
    /// The description the <c>DISCONNECT</c> carries.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// A <see cref="SshDisconnectReason.ProtocolError"/> refusal, described <c>Protocol error</c>.
    /// </summary>
    /// <param name="note">What the operator is told about the refusal.</param>
    /// <returns>The exception.</returns>
    public static SshDisconnectRequiredException ProtocolError(string note) =>
        new(SshDisconnectReason.ProtocolError, "Protocol error", note);
}
