using Surl.Content;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// What a host hands <see cref="SurlServerComposition.ComposeAsync"/> besides the parsed command line
/// (ADR-0099, decision 2): how listeners are started, how the data directory is probed and locked,
/// the one clock, the <c>SURL_EXTENSIONS</c> switch the host read, and the optional seams a test
/// replaces.
/// </summary>
/// <param name="CreateListenerFactory">
/// Creates the factory that starts the listeners, given the process's TLS settings
/// (<see langword="null"/> when no listen URL is TLS from the first byte); <c>surl</c> passes one that
/// creates a <c>SocketListenerFactory</c>.
/// </param>
/// <param name="CanOpenDataDirectory">
/// Tells whether the data directory, as given with <c>--directory</c>, can be opened; never called
/// without <c>--directory</c>. <c>surl</c> passes <see cref="DataDirectoryProbe.CanOpen"/>.
/// </param>
/// <param name="TakeDataDirectoryLock">
/// Takes the data directory's <c>.surl/lock</c> (ADR-0031, decision 7), given the data directory as
/// given with <c>--directory</c>; never called without <c>--directory</c>. <c>surl</c> passes
/// <see cref="DataDirectoryLock.Take"/>.
/// </param>
/// <param name="TimeProvider">The one clock every exchange runs on.</param>
/// <param name="AreExtensionsTurnedOn">
/// Whether the extended protocol servers are served, as the host read <c>SURL_EXTENSIONS</c>
/// through <see cref="ExtendedProtocolServers.AreTurnedOn"/> (ADR-0083, decision 6).
/// </param>
/// <remarks>
/// Public because more than one host calls it, but not a supported programming interface: it may
/// change in any release (ADR-0099, decision 1).
/// </remarks>
public sealed record SurlServerCompositionSeams(
    Func<ServerTlsSettings?, IListenerFactory> CreateListenerFactory,
    Func<string, bool> CanOpenDataDirectory,
    Func<string, DataDirectoryLockOutcome> TakeDataDirectoryLock,
    TimeProvider TimeProvider,
    bool AreExtensionsTurnedOn)
{
    /// <summary>
    /// The file system the data directory is served and its service state kept through with
    /// <c>--directory</c>; a <see cref="DiskContentFileSystem"/> when <see langword="null"/>, which
    /// is what <c>surl</c> passes.
    /// </summary>
    public IContentFileSystem? DataDirectoryFileSystem { get; init; }

    /// <summary>
    /// The file system served without <c>--directory</c>; a new, empty
    /// <see cref="InMemoryContentFileSystem"/> when <see langword="null"/>, which is what <c>surl</c>
    /// passes. It is how a host serves files its caller supplies in memory.
    /// </summary>
    public IContentFileSystem? ContentWithoutDataDirectory { get; init; }

    /// <summary>
    /// Reads the bytes of a file read at start, given its path as given, before any listener binds:
    /// the <c>--user-file</c>, each <c>--authorized-keys</c> file, the <c>--keytab</c> file and each
    /// <c>--hostkey</c> and <c>--hostcert</c> file; <see cref="File.ReadAllBytes(string)"/> when
    /// <see langword="null"/>, which is what <c>surl</c> passes.
    /// </summary>
    public Func<string, byte[]>? ReadStartFile { get; init; }

    /// <summary>
    /// Creates what opens the FTP server's data connections, given the process's TLS settings
    /// (ADR-0052, decision 9); a <c>SocketDataConnectionOpener</c> on the one clock when
    /// <see langword="null"/>, which is what <c>surl</c> passes.
    /// </summary>
    public Func<ServerTlsSettings?, IDataConnectionOpener>? CreateDataConnectionOpener { get; init; }

    /// <summary>
    /// Builds the extended protocol servers while <see cref="AreExtensionsTurnedOn"/>, and is never
    /// called otherwise; <see cref="ExtendedProtocolServers.Compose"/> when <see langword="null"/>,
    /// which is what <c>surl</c> passes.
    /// </summary>
    public Func<ExtendedProtocolServerServices, IReadOnlyList<IProtocolServer>>? ComposeExtendedProtocolServers { get; init; }
}
