using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// Listens for connections on the Unix domain socket a listen URL names in
/// <see cref="ListenUrl.UnixSocket"/> - a socket file, or on Linux an abstract name - and hands
/// each accepted connection out as an <see cref="IConnection"/> (ADR-0094, decisions 3, 5 and 6).
/// </summary>
/// <remarks>
/// The socket file is created by the bind, made owner-only (<c>0600</c>) before
/// <c>listen</c> on Linux and macOS, and deleted when the listener stops - only while the path
/// still names the file this listener created. Something already at the path is never deleted:
/// the bind is refused <see cref="ListenerBindFailure.AddressInUse"/>. The decisions live in
/// <see cref="UnixSocketBindRules"/>, which the fast tests cover on every platform's arm; every
/// member here calls a socket or the file system, and is exercised by
/// <c>UnixSocketConnectionListenerTests</c>, fast tests on a socket file under the temporary
/// directory. <see cref="AcceptAsync"/> is meant for one caller at a time.
/// </remarks>
public sealed class UnixSocketConnectionListener : IConnectionListener
{
    private readonly Socket listeningSocket;
    private readonly UnixDomainSocketEndPoint localEndPoint;
    private readonly string? createdFilePath;
    private readonly DateTime createdFileWriteTime;
    private readonly AcceptRace<Socket> acceptRace;
    private readonly ServerTlsHandshake? tlsHandshake;
    private readonly TimeProvider timeProvider;

    // Keeps the bound socket and reads the created file's write time, its identity at stop.
    [ExcludeFromCodeCoverage(Justification = "Constructed only over a bound socket; covered by UnixSocketConnectionListenerTests.")]
    private UnixSocketConnectionListener(
        ListenUrl listenUrl, Socket listeningSocket, UnixDomainSocketEndPoint localEndPoint, ServerTlsSettings? tlsSettings, TimeProvider timeProvider)
    {
        ListenUrl = listenUrl;
        this.listeningSocket = listeningSocket;
        this.localEndPoint = localEndPoint;
        createdFilePath = listenUrl.UnixSocket!.IsAbstract ? null : listenUrl.UnixSocket.Name;
        createdFileWriteTime = createdFilePath is null ? default : File.GetLastWriteTimeUtc(createdFilePath);
        tlsHandshake = ServerTlsHandshake.ForListener(tlsSettings, listenUrl);
        this.timeProvider = timeProvider;
        acceptRace = new AcceptRace<Socket>(1, AcceptFromSocketAsync, ReleaseSocket);
    }

    /// <inheritdoc/>
    public ListenUrl ListenUrl
    {
        [ExcludeFromCodeCoverage(Justification = "Set only by the socket-reading constructor; covered by UnixSocketConnectionListenerTests.")]
        get;
    }

    /// <inheritdoc/>
    public IReadOnlyList<EndPoint> BoundEndPoints
    {
        [ExcludeFromCodeCoverage(Justification = "Reads the bound endpoint; covered by UnixSocketConnectionListenerTests.")]
        get => [localEndPoint];
    }

    /// <summary>
    /// Binds the Unix domain socket <paramref name="listenUrl"/> names in
    /// <see cref="ListenUrl.UnixSocket"/> and starts listening for connections, giving every
    /// accepted connection the TLS settings its <see cref="IConnection.UpgradeToTlsAsync"/> uses
    /// (ADR-0010), with the ALPN protocol IDs the listen URL's scheme offers.
    /// </summary>
    /// <param name="listenUrl">What to listen on; its <see cref="ListenUrl.UnixSocket"/> is set.</param>
    /// <param name="tlsSettings">The process's TLS settings, or <see langword="null"/> for connections that cannot be secured.</param>
    /// <param name="cancellationToken">Cuts the start off.</param>
    /// <param name="timeProvider">Times each accepted connection's peek waits and closes; <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    /// <returns>The listener; its listen URL is <paramref name="listenUrl"/>, with no bound port.</returns>
    /// <exception cref="ArgumentException"><paramref name="listenUrl"/> names no Unix socket.</exception>
    /// <exception cref="ListenerBindException">
    /// The socket could not be bound: an abstract name off Linux, a path too long for the
    /// platform, something already at the path, a directory that is not there, or the
    /// operating system refused the bind. Nothing stays bound and no file is left behind.
    /// </exception>
    [ExcludeFromCodeCoverage(Justification = "Binds a socket; covered by UnixSocketConnectionListenerTests.")]
    public static ValueTask<UnixSocketConnectionListener> StartAsync(
        ListenUrl listenUrl, ServerTlsSettings? tlsSettings, CancellationToken cancellationToken, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(listenUrl);
        var address = listenUrl.UnixSocket
            ?? throw new ArgumentException("The listen URL names no Unix socket.", nameof(listenUrl));
        cancellationToken.ThrowIfCancellationRequested();

        var refusal = UnixSocketBindRules.FindRefusalBeforeBinding(
            address, OperatingSystem.IsLinux(), OperatingSystem.IsMacOS(), PathExists);

        if (refusal is { } failure)
        {
            throw new ListenerBindException(listenUrl, null, failure, null);
        }

        var endPoint = new UnixDomainSocketEndPoint(UnixSocketBindRules.EndPointNameOf(address));
        var socket = BindListeningSocket(listenUrl, endPoint);

        return ValueTask.FromResult(new UnixSocketConnectionListener(listenUrl, socket, endPoint, tlsSettings, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The listener was disposed.</exception>
    /// <exception cref="IOException">
    /// The listening socket failed. An accept that failed for one client only is absorbed, and
    /// the listener accepts again (ADR-0022).
    /// </exception>
    [ExcludeFromCodeCoverage(Justification = "Accepts a socket; covered by UnixSocketConnectionListenerTests.")]
    public async ValueTask<IConnection> AcceptAsync(CancellationToken cancellationToken)
    {
        var accepted = await acceptRace.AcceptNextAsync(cancellationToken);

        return new StreamConnection(
            new NetworkStream(accepted, ownsSocket: true),
            localEndPoint,
            UnixSocketBindRules.DescribePeer(RemoteEndPointOf(accepted)),
            new SocketTransportControl(accepted),
            tlsHandshake,
            timeProvider);
    }

    /// <summary>
    /// Stops listening, closes the listening socket and deletes the socket file it created, if
    /// the path still names that file. Connections already accepted are unaffected. Calling it
    /// twice is harmless.
    /// </summary>
    /// <returns>A task that completes once the socket is closed and its file deleted.</returns>
    [ExcludeFromCodeCoverage(Justification = "Disposes a socket and deletes its file; covered by UnixSocketConnectionListenerTests.")]
    public async ValueTask DisposeAsync()
    {
        var stopping = acceptRace.StopAsync();
        listeningSocket.Dispose();
        await stopping;
        DeleteCreatedFile(createdFilePath, createdFileWriteTime);
    }

    // Creates an AF_UNIX stream socket, binds it, makes its file owner-only off Windows, and
    // listens. A failure after the bind disposes the socket and deletes the file it made.
    [ExcludeFromCodeCoverage(Justification = "Binds a socket; covered by UnixSocketConnectionListenerTests.")]
    private static Socket BindListeningSocket(ListenUrl listenUrl, UnixDomainSocketEndPoint endPoint)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        var bound = false;

        try
        {
            socket.Bind(new UnixSocketFileEndPoint(endPoint));
            bound = true;
            MakeOwnerOnly(listenUrl.UnixSocket!);
            socket.Listen();

            return socket;
        }
        catch (Exception exception) when (exception is SocketException or IOException or UnauthorizedAccessException)
        {
            socket.Dispose();
            DeleteFileMadeByFailedStart(listenUrl.UnixSocket!, bound);

            throw new ListenerBindException(listenUrl, endPoint, ClassifyBindFailure(exception), exception);
        }
    }

    // Sets the socket file's mode to 0600 on Linux and macOS; Windows keeps its directory's ACL.
    [ExcludeFromCodeCoverage(Justification = "Calls File.SetUnixFileMode off Windows; covered by UnixSocketConnectionListenerTests.")]
    private static void MakeOwnerOnly(UnixSocketAddress address)
    {
        if (!address.IsAbstract && !OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(address.Name, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [ExcludeFromCodeCoverage(Justification = "Deletes a file; covered by UnixSocketConnectionListenerTests.")]
    private static void DeleteFileMadeByFailedStart(UnixSocketAddress address, bool bound)
    {
        if (bound && !address.IsAbstract)
        {
            TryDelete(address.Name);
        }
    }

    // Deletes the socket file only while the path still names the file this listener created:
    // something there with another write time was put there by someone else, and is left alone.
    // Disposing the socket may already have removed it, as .NET does on Windows.
    [ExcludeFromCodeCoverage(Justification = "Deletes a file; covered by UnixSocketConnectionListenerTests.")]
    private static void DeleteCreatedFile(string? path, DateTime createdWriteTime)
    {
        if (path is not null && File.Exists(path) && File.GetLastWriteTimeUtc(path) == createdWriteTime)
        {
            TryDelete(path);
        }
    }

    [ExcludeFromCodeCoverage(Justification = "Deletes a file; covered by UnixSocketConnectionListenerTests.")]
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Stopping must not throw; a file that cannot be deleted is left, as after a crash.
        }
    }

    [ExcludeFromCodeCoverage(Justification = "Reads the file system; covered by UnixSocketConnectionListenerTests.")]
    private static bool PathExists(string path) => File.Exists(path) || Directory.Exists(path);

    [ExcludeFromCodeCoverage(Justification = "Classifies a socket or file failure; covered by UnixSocketConnectionListenerTests.")]
    private static ListenerBindFailure ClassifyBindFailure(Exception exception) => exception switch
    {
        SocketException socketException => BindFailureClassifier.Classify(socketException.SocketErrorCode),
        UnauthorizedAccessException => ListenerBindFailure.PermissionDenied,
        _ => ListenerBindFailure.Other,
    };

    // Reads Socket.RemoteEndPoint; a client gone before it could be read is named unnamed.
    [ExcludeFromCodeCoverage(Justification = "Reads Socket.RemoteEndPoint; covered by UnixSocketConnectionListenerTests.")]
    private static EndPoint? RemoteEndPointOf(Socket socket)
    {
        try
        {
            return socket.RemoteEndPoint;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    [ExcludeFromCodeCoverage(Justification = "Disposes a socket; covered by UnixSocketConnectionListenerTests.")]
    private static void ReleaseSocket(Socket socket) => socket.Dispose();

    [ExcludeFromCodeCoverage(Justification = "Accepts on a socket; covered by UnixSocketConnectionListenerTests.")]
    private Task<Socket> AcceptFromSocketAsync(int index, CancellationToken cancellationToken) =>
        listeningSocket.AcceptAsync(cancellationToken).AsTask();
}
