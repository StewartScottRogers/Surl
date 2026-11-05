using Surl.Composition;
using Surl.Content;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// What <see cref="SurlServer"/> hands the composition from the host's side: how listeners start,
/// how the data directory is probed and locked, and where <c>SURL_EXTENSIONS</c> is read.
/// <see cref="Production"/> is what <see cref="SurlServer.StartAsync(SurlServerOptions, CancellationToken)"/>
/// uses; a test passes fakes so no socket or file is opened.
/// </summary>
/// <param name="CreateListenerFactory">Creates the factory that starts the listeners, given the server's TLS settings.</param>
/// <param name="CanOpenDataDirectory">Tells whether the data directory can be opened.</param>
/// <param name="TakeDataDirectoryLock">Takes the data directory's <c>.surl/lock</c>.</param>
/// <param name="EnvironmentVariables">Reads <c>SURL_EXTENSIONS</c> when the options leave it to the process.</param>
internal sealed record SurlServerHostSeams(
    Func<ServerTlsSettings?, IListenerFactory> CreateListenerFactory,
    Func<string, bool> CanOpenDataDirectory,
    Func<string, DataDirectoryLockOutcome> TakeDataDirectoryLock,
    IEnvironmentVariableReader EnvironmentVariables)
{
    /// <summary>
    /// The seams <c>surl</c> uses too: socket listeners, the disk probe and lock, and the process's
    /// environment.
    /// </summary>
    public static SurlServerHostSeams Production { get; } = new(
        CreateSocketListenerFactory, DataDirectoryProbe.CanOpen, DataDirectoryLock.Take, new ProcessEnvironmentVariableReader());

    /// <summary>The file system a data directory is served through; the disk when <see langword="null"/>.</summary>
    public IContentFileSystem? DataDirectoryFileSystem { get; init; }

    /// <summary>Creates a <see cref="SocketListenerFactory"/> securing connections with <paramref name="tlsSettings"/>.</summary>
    /// <param name="tlsSettings">The server's TLS settings, or <see langword="null"/> for none.</param>
    /// <returns>The factory.</returns>
    internal static IListenerFactory CreateSocketListenerFactory(ServerTlsSettings? tlsSettings) =>
        new SocketListenerFactory(tlsSettings);
}
