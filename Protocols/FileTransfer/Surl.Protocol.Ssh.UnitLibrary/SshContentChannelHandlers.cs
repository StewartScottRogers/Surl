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
    public ISshChannelHandler? ForScp(SshScpCommand command, string? accountName, ExchangeContext context)
    {
        var caller = CallerOn(accountName, "scp");

        return command.IsSource ? new ScpDownloadHandler(store, caller, command, context) : new ScpUploadHandler(store, caller, command, context);
    }

    /// <inheritdoc/>
    public ISshChannelHandler? ForSftp(string? accountName, ExchangeContext context) =>
        new SftpSession(store, CallerOn(accountName, "sftp"), context);

    // ADR-0102 decision 2.2: a login that names no account is an anonymous caller.
    private static ContentCaller CallerOn(string? accountName, string scheme) =>
        string.IsNullOrEmpty(accountName) ? ContentCaller.Anonymous(scheme) : ContentCaller.ForAccount(accountName, scheme);
}
