using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// No handler for anything: every <c>exec</c> and <c>subsystem</c> is refused. What the server
/// uses when it is given no content store to serve.
/// </summary>
internal sealed class SshNoChannelHandlers : ISshChannelHandlers
{
    /// <summary>
    /// The one instance.
    /// </summary>
    public static SshNoChannelHandlers Instance { get; } = new();

    /// <inheritdoc/>
    public ISshChannelHandler? ForScp(SshScpCommand command, ExchangeContext context) => null;

    /// <inheritdoc/>
    public ISshChannelHandler? ForSftp(ExchangeContext context) => null;
}
