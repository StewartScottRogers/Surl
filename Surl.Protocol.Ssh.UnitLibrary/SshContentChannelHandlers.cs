using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The handlers that serve a content store over SSH (ADR-0054): the <c>sftp</c> subsystem as an
/// <see cref="SftpSession"/> reading the store. SCP is BL-164's, so an <c>exec</c> of an SCP command
/// is refused until it is built.
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
    public ISshChannelHandler? ForScp(SshScpCommand command, ExchangeContext context) => null;

    /// <inheritdoc/>
    public ISshChannelHandler? ForSftp(ExchangeContext context) => new SftpSession(store, context);
}
