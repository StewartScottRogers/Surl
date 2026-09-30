namespace Surl.Protocol.Ssh;

/// <summary>
/// The handlers the server hands an accepted session channel to (ADR-0054, decisions 2 and 5):
/// an <c>exec</c> whose command is an SCP command, and the <c>sftp</c> subsystem.
/// </summary>
internal interface ISshChannelHandlers
{
    /// <summary>
    /// The handler for an <c>exec</c> of <paramref name="command"/>.
    /// </summary>
    /// <param name="command">The SCP command the <c>exec</c> named.</param>
    /// <returns>The handler, or <see langword="null"/> when SCP is not served: the request is refused.</returns>
    ISshChannelHandler? ForScp(SshScpCommand command);

    /// <summary>
    /// The handler for the <c>sftp</c> subsystem.
    /// </summary>
    /// <returns>The handler, or <see langword="null"/> when SFTP is not served: the request is refused.</returns>
    ISshChannelHandler? ForSftp();
}
