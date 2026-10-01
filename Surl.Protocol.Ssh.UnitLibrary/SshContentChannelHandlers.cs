using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The handlers that serve a content store over SSH (ADR-0054): the <c>sftp</c> subsystem as an
/// <see cref="SftpSession"/>, and an <c>exec</c> of an SCP command as an <see cref="ScpDownloadHandler"/>
/// (<c>-f</c>) or an <see cref="ScpUploadHandler"/> (<c>-t</c>).
/// </summary>
internal sealed class SshContentChannelHandlers : ISshChannelHandlers
{
    private readonly ContentStore store;

    /// <summary>
    /// Creates the handlers for <paramref name="store"/>.
    /// </summary>
    /// <param name="store">The content store served.</param>
    public SshContentChannelHandlers(ContentStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <inheritdoc/>
    public ISshChannelHandler? ForScp(SshScpCommand command, ExchangeContext context) =>
        command.IsSource ? new ScpDownloadHandler(store, command, context) : new ScpUploadHandler(store, command, context);

    /// <inheritdoc/>
    public ISshChannelHandler? ForSftp(ExchangeContext context) => new SftpSession(store, context);
}
