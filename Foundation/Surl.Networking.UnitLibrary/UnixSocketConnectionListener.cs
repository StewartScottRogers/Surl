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
    private UnixSocketConnectionListener(
        ListenUrl listenUrl, Socket listeningSocket, UnixDomainSocketEndPoint localEndPoint, ServerTlsSettings? tlsSettings, TimeProvider timeProvider)
    {
        ListenUrl = listenUrl;
        this.listeningSocket = listeningSocket;
        this.localEndPoint = localEndPoint;
        createdFilePath = CreatedFilePathOf(listenUrl.UnixSocket!);
        createdFileWriteTime = WriteTimeOf(createdFilePath);
        tlsHandshake = ServerTlsHandshake.ForListener(tlsSettings, listenUrl);
        this.timeProvider = timeProvider;
        acceptRace = new AcceptRace<Socket>(1, AcceptFromSocketAsync, ReleaseSocket);
    }

    /// <inheritdoc/>
    public ListenUrl ListenUrl
    {
        get;
    }

    /// <inheritdoc/>
    public IReadOnlyList<EndPoint> BoundEndPoints
    {
        get => [localEndPoint];
    }

    /// <summary>
    /// Binds the Unix domain socket <paramref name="listenUrl"/> names in
    /// <see cref="ListenUrl.UnixSocket"/> and starts listening for connections, giving every
    /// accepted connection the TLS settings its <see cref="IConnection.UpgradeToTlsAsync"/> uses
    /// (ADR-0010), with the ALPN protocol IDs the listen URL's scheme offers.
    /// </summary>
    /// <param name="listenUrl">What to listen on; its <see cref="ListenUrl.UnixSocket"/> is set.</param>
    /// <param name="tlsSettings">The TLS settings for accepted connections, or <see langword="null"/> for connections that cannot be secured.</param>
    /// <param name="cancellationToken">Cuts the start off.</param>
    /// <param name="timeProvider">Times each accepted connection's peek waits and closes; <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    /// <returns>The listener; its listen URL is <paramref name="listenUrl"/>, with no bound port.</returns>
    /// <exception cref="ArgumentException"><paramref name="listenUrl"/> names no Unix socket.</exception>
    /// <exception cref="ListenerBindException">
    /// The socket could not be bound: an abstract name off Linux, a path too long for the
    /// platform, something already at the path, a directory that is not there, or the
    /// operating system refused the bind. Nothing stays bound and no file is left behind.
    /// </exception>
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
        var socket = BindListeningSocket(listenUrl, endPoint, MakeOwnerOnly);

        return ValueTask.FromResult(new UnixSocketConnectionListener(listenUrl, socket, endPoint, tlsSettings, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The listener was disposed.</exception>
    /// <exception cref="IOException">
    /// The listening socket failed. An accept that failed for one client only is absorbed, and
    /// the listener accepts again (ADR-0022).
    /// </exception>
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
    public async ValueTask DisposeAsync()
    {
        var stopping = acceptRace.StopAsync();
        listeningSocket.Dispose();
        await stopping;
        DeleteCreatedFile(createdFilePath, createdFileWriteTime);
    }

    // Creates an AF_UNIX stream socket, binds it, makes its file owner-only off Windows, and
    // listens. A failure after the bind disposes the socket and deletes the file it made.
    internal static Socket BindListeningSocket(ListenUrl listenUrl, UnixDomainSocketEndPoint endPoint, Action<UnixSocketAddress> makeOwnerOnly)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        var bound = false;

        try
        {
            socket.Bind(new UnixSocketFileEndPoint(endPoint));
            bound = true;
            makeOwnerOnly(listenUrl.UnixSocket!);
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

    // The file the bind created, or null for an abstract name, which has none.
    internal static string? CreatedFilePathOf(UnixSocketAddress address) => address.IsAbstract ? null : address.Name;

    // The write time that identifies the created file at stop; default when there is no file.
    internal static DateTime WriteTimeOf(string? createdFilePath) =>
        createdFilePath is null ? default : File.GetLastWriteTimeUtc(createdFilePath);

    // Sets the socket file's mode to 0600 on Linux and macOS; Windows keeps its directory's ACL.
    [ExcludeFromCodeCoverage(Justification = "File.SetUnixFileMode runs only off Windows, and the abstract-name arm is refused on Windows before any bind, so no fast test on Windows reaches either.")]
    private static void MakeOwnerOnly(UnixSocketAddress address)
    {
        if (!address.IsAbstract && !OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(address.Name, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    internal static void DeleteFileMadeByFailedStart(UnixSocketAddress address, bool bound)
    {
        if (bound && !address.IsAbstract)
        {
            TryDelete(address.Name);
        }
    }

    // Deletes the socket file only while the path still names the file this listener created:
    // something there with another write time was put there by someone else, and is left alone.
    // Disposing the socket may already have removed it, as .NET does on Windows.
    private static void DeleteCreatedFile(string? path, DateTime createdWriteTime)
    {
        if (path is not null && File.Exists(path) && File.GetLastWriteTimeUtc(path) == createdWriteTime)
        {
            TryDelete(path);
        }
    }

    internal static void TryDelete(string path)
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

    private static bool PathExists(string path) => File.Exists(path) || Directory.Exists(path);

    internal static ListenerBindFailure ClassifyBindFailure(Exception exception) => exception switch
    {
        SocketException socketException => BindFailureClassifier.Classify(socketException.SocketErrorCode),
        UnauthorizedAccessException => ListenerBindFailure.PermissionDenied,
        _ => ListenerBindFailure.Other,
    };

    // Reads Socket.RemoteEndPoint; a client gone before it could be read is named unnamed.
    [ExcludeFromCodeCoverage(Justification = "The SocketException arm needs a peer that vanished between accept and the read; no fast test can make one happen on demand.")]
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

    internal static void ReleaseSocket(Socket socket) => socket.Dispose();

    private Task<Socket> AcceptFromSocketAsync(int index, CancellationToken cancellationToken) =>
        Task.Factory.StartNew(() => AcceptBlocking(cancellationToken), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    // A blocking accept on its own thread, not Socket.AcceptAsync (BL-1565): on macOS .NET's async
    // accept throws ArgumentException ("Destination is too short") from FinishOperationAccept on a
    // SocketAsyncEngine thread when the peer's AF_UNIX address is one it cannot copy - a client bound
    // to a named socket - and nothing can catch it there, so the process ended. The blocking accept
    // throws on this thread instead; that client is dropped and the accept goes on. The wait is a
    // short poll for a queued client, then an accept that returns at once, so the loop sees the
    // stop token within PollMicroseconds (BL-1568): on macOS disposing the socket does not wake a
    // thread blocked in Accept(). A client the runtime cannot accept is waited out for
    // RetryPauseMilliseconds, so one bad client cannot spin the loop.
    private Socket AcceptBlocking(CancellationToken cancellationToken)
    {
        Socket? accepted = null;

        while (accepted is null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            accepted = listeningSocket.Poll(PollMicroseconds, SelectMode.SelectRead)
                ? AcceptOrDropClient(cancellationToken)
                : null;
        }

        return accepted;
    }

    // Accepts the queued client, or null after waiting out one the runtime could not accept.
    [ExcludeFromCodeCoverage(Justification = "The ArgumentException arm is thrown only by macOS's runtime for a client bound to a named socket; no fast test on Windows reaches it.")]
    private Socket? AcceptOrDropClient(CancellationToken cancellationToken)
    {
        try
        {
            return listeningSocket.Accept();
        }
        catch (ArgumentException)
        {
            // The runtime could not copy this peer's address: drop it, accept the next.
            cancellationToken.WaitHandle.WaitOne(RetryPauseMilliseconds);

            return null;
        }
    }

    private const int PollMicroseconds = 100_000;

    private const int RetryPauseMilliseconds = 50;
}
