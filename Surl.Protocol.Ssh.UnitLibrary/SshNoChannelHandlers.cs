namespace Surl.Protocol.Ssh;

/// <summary>
/// No handler for anything: every <c>exec</c> and <c>subsystem</c> is refused. What the server
/// uses until SCP (BL-164) and SFTP (BL-165) are built.
/// </summary>
internal sealed class SshNoChannelHandlers : ISshChannelHandlers
{
    /// <summary>
    /// The one instance.
    /// </summary>
    public static SshNoChannelHandlers Instance { get; } = new();

    /// <inheritdoc/>
    public ISshChannelHandler? ForScp(SshScpCommand command) => null;

    /// <inheritdoc/>
    public ISshChannelHandler? ForSftp() => null;
}
