namespace Surl.Protocol.Ssh;

/// <summary>
/// Thrown where the exchange ends with no reply: the client closed the connection, or sent
/// its own <c>SSH_MSG_DISCONNECT</c> (ADR-0051, decision 9).
/// </summary>
internal sealed class SshExchangeEndedException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="note">What the operator is told, or <see langword="null"/> when the end needs no note.</param>
    public SshExchangeEndedException(string? note)
        : base(note ?? "The client ended the SSH exchange.")
    {
        Note = note;
    }

    /// <summary>
    /// What the operator is told, or <see langword="null"/> when the end needs no note.
    /// </summary>
    public string? Note { get; }
}
