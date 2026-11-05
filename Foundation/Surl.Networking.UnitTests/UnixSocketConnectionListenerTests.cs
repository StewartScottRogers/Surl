using System.Net.Sockets;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// <see cref="UnixSocketConnectionListener"/> on real <c>AF_UNIX</c> sockets (ADR-0094, decisions
/// 3, 5 and 6): a socket file under <see cref="Path.GetTempPath"/>, named short enough for macOS's
/// 103 bytes, reached by a client <see cref="Socket"/> in the test. Every platform CI runs on has
/// <c>AF_UNIX</c> (Windows since 10 1803), and nothing leaves the machine, so these are fast
/// tests; only the abstract-name tests are split by platform.
/// </summary>
[TestClass]
public sealed class UnixSocketConnectionListenerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task StartAsync_SocketFile_CreatesTheFileAndReportsItsEndPoint()
    {
        var path = NewSocketPath();
        var listenUrl = UnixListenUrl(path, false);

        await using var listener = await UnixSocketConnectionListener.StartAsync(listenUrl, null, TestContext.CancellationToken);

        Assert.IsTrue(File.Exists(path));
        Assert.AreSame(listenUrl, listener.ListenUrl);
        Assert.IsNull(listener.ListenUrl.BoundPort);
        Assert.HasCount(1, listener.BoundEndPoints);
        Assert.IsInstanceOfType<UnixDomainSocketEndPoint>(listener.BoundEndPoints[0]);
        Assert.AreEqual(AddressFamily.Unix, listener.BoundEndPoints[0].AddressFamily);
    }

    [TestMethod]
    public async Task AcceptAsync_SocketFile_ExchangesBytesBothWaysWithAnUnnamedPeer()
    {
        var path = NewSocketPath();
        await using var listener = await UnixSocketConnectionListener.StartAsync(
            UnixListenUrl(path, false), null, TestContext.CancellationToken);

        await ExchangeBytesAsync(listener, new UnixDomainSocketEndPoint(path));
    }

    [TestMethod]
    public async Task DisposeAsync_SocketFile_DeletesTheFileAndStopsListening()
    {
        var path = NewSocketPath();
        var listener = await UnixSocketConnectionListener.StartAsync(
            UnixListenUrl(path, false), null, TestContext.CancellationToken);

        await listener.DisposeAsync();
        await listener.DisposeAsync();

        Assert.IsFalse(File.Exists(path));
        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await Assert.ThrowsExactlyAsync<SocketException>(
            () => client.ConnectAsync(new UnixDomainSocketEndPoint(path), TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task DisposeAsync_PathReplacedByAnotherFile_LeavesThatFileAlone()
    {
        var path = NewSocketPath();
        var listener = await UnixSocketConnectionListener.StartAsync(
            UnixListenUrl(path, false), null, TestContext.CancellationToken);
        File.Delete(path);
        await File.WriteAllTextAsync(path, "someone else's", TestContext.CancellationToken);
        File.SetLastWriteTimeUtc(path, new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        try
        {
            await listener.DisposeAsync();

            Assert.IsTrue(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task StartAsync_SocketFile_IsOwnerReadAndWriteOnly()
    {
        var path = NewSocketPath();

        await using var listener = await UnixSocketConnectionListener.StartAsync(
            UnixListenUrl(path, false), null, TestContext.CancellationToken);

        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    [TestMethod]
    public async Task StartAsync_SocketFileAlreadyPresent_ThrowsAddressInUseAndLeavesTheFile()
    {
        var path = NewSocketPath();
        await using var first = await UnixSocketConnectionListener.StartAsync(
            UnixListenUrl(path, false), null, TestContext.CancellationToken);

        var exception = await Assert.ThrowsExactlyAsync<ListenerBindException>(
            () => UnixSocketConnectionListener.StartAsync(UnixListenUrl(path, false), null, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(ListenerBindFailure.AddressInUse, exception.Failure);
        Assert.IsTrue(File.Exists(path));
        await ExchangeBytesAsync(first, new UnixDomainSocketEndPoint(path));
    }

    [TestMethod]
    public async Task StartAsync_RegularFileAtThePath_ThrowsAddressInUseAndLeavesTheFile()
    {
        var path = NewSocketPath();
        await File.WriteAllTextAsync(path, "not a socket", TestContext.CancellationToken);

        try
        {
            var exception = await Assert.ThrowsExactlyAsync<ListenerBindException>(
                () => UnixSocketConnectionListener.StartAsync(UnixListenUrl(path, false), null, TestContext.CancellationToken).AsTask());

            Assert.AreEqual(ListenerBindFailure.AddressInUse, exception.Failure);
            Assert.AreEqual("not a socket", await File.ReadAllTextAsync(path, TestContext.CancellationToken));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task StartAsync_PathLongerThanThePlatformsLimit_ThrowsUnixSocketPathTooLong()
    {
        var limit = UnixSocketBindRules.MaximumPathBytes(OperatingSystem.IsMacOS());
        var directory = Path.GetTempPath();
        var path = Path.Combine(directory, new string('y', limit + 1 - directory.Length - 5) + ".sock");

        var exception = await Assert.ThrowsExactlyAsync<ListenerBindException>(
            () => UnixSocketConnectionListener.StartAsync(UnixListenUrl(path, false), null, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(limit + 1, Encoding.UTF8.GetByteCount(path));
        Assert.AreEqual(ListenerBindFailure.UnixSocketPathTooLong, exception.Failure);
        Assert.IsNull(exception.EndPoint);
        Assert.AreEqual(SurlExitCode.BindFailed, BindFailedFor(exception.Failure));
        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public async Task StartAsync_DirectoryNotThere_ThrowsAddressNotAvailable()
    {
        var path = Path.Combine(Path.GetTempPath(), "surl-" + Guid.NewGuid().ToString("N")[..8], "s.sock");

        var exception = await Assert.ThrowsExactlyAsync<ListenerBindException>(
            () => UnixSocketConnectionListener.StartAsync(UnixListenUrl(path, false), null, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(ListenerBindFailure.AddressNotAvailable, exception.Failure);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Linux)]
    public async Task StartAsync_AbstractNameOffLinux_ThrowsAbstractUnixSocketUnsupported()
    {
        var exception = await Assert.ThrowsExactlyAsync<ListenerBindException>(
            () => UnixSocketConnectionListener.StartAsync(
                UnixListenUrl(NewAbstractName(), true), null, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(ListenerBindFailure.AbstractUnixSocketUnsupported, exception.Failure);
        Assert.AreEqual(SurlExitCode.BindFailed, BindFailedFor(exception.Failure));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public async Task AcceptAsync_AbstractNameOnLinux_ExchangesBytesBothWays()
    {
        var name = NewAbstractName();
        await using var listener = await UnixSocketConnectionListener.StartAsync(
            UnixListenUrl(name, true), null, TestContext.CancellationToken);

        await ExchangeBytesAsync(listener, new UnixDomainSocketEndPoint("\0" + name));
    }

    [TestMethod]
    public async Task StartAsync_ListenUrlWithNoUnixSocket_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => UnixSocketConnectionListener.StartAsync(
                new ListenUrl("http", "localhost", 80), null, TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task StartConnectionListenerAsync_ListenUrlWithAUnixSocket_ListensOnIt()
    {
        var path = NewSocketPath();
        var factory = new SocketListenerFactory();

        await using var listener = await factory.StartConnectionListenerAsync(UnixListenUrl(path, false), TestContext.CancellationToken);

        Assert.IsInstanceOfType<UnixSocketConnectionListener>(listener);
        await ExchangeBytesAsync(listener, new UnixDomainSocketEndPoint(path));
    }

    // The exit code Surl.Core's ServingEngine gives every ListenerBindFailure but HostNotFound
    // (ADR-0004, section 6), which ADR-0094 names for each Unix socket failure.
    private static SurlExitCode BindFailedFor(ListenerBindFailure failure) =>
        failure == ListenerBindFailure.HostNotFound ? SurlExitCode.CouldNotResolveHost : SurlExitCode.BindFailed;

    private static ListenUrl UnixListenUrl(string name, bool isAbstract) =>
        new("http", "localhost", 80) { UnixSocket = new UnixSocketAddress(name, isAbstract) };

    private static string NewSocketPath() =>
        Path.Combine(Path.GetTempPath(), "surl-" + Guid.NewGuid().ToString("N")[..8] + ".sock");

    private static string NewAbstractName() => "surl-test-" + Guid.NewGuid().ToString("N")[..8];

    private async Task ExchangeBytesAsync(IConnectionListener listener, UnixDomainSocketEndPoint target)
    {
        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await client.ConnectAsync(target, TestContext.CancellationToken);
        await using var connection = await listener.AcceptAsync(TestContext.CancellationToken);
        await client.SendAsync(Encoding.ASCII.GetBytes("ping"), TestContext.CancellationToken);
        var received = new byte[4];
        await ReadExactlyAsync(connection, received);
        await connection.WriteAsync(Encoding.ASCII.GetBytes("pong"), TestContext.CancellationToken);
        await connection.CompleteWritesAsync(TestContext.CancellationToken);
        var answered = new byte[4];
        await ReceiveExactlyAsync(client, answered);

        Assert.AreEqual("ping", Encoding.ASCII.GetString(received));
        Assert.AreEqual("pong", Encoding.ASCII.GetString(answered));
        Assert.AreSame(UnnamedUnixSocketPeer.Instance, connection.RemoteEndPoint);
        Assert.AreSame(listener.BoundEndPoints[0], connection.LocalEndPoint);
        client.Shutdown(SocketShutdown.Send);
    }

    private async Task ReadExactlyAsync(IConnection connection, byte[] buffer)
    {
        var filled = 0;

        while (filled < buffer.Length)
        {
            var read = await connection.ReadAsync(buffer.AsMemory(filled), TestContext.CancellationToken);
            Assert.AreNotEqual(0, read, "The connection ended early.");
            filled += read;
        }
    }

    private async Task ReceiveExactlyAsync(Socket socket, byte[] buffer)
    {
        var filled = 0;

        while (filled < buffer.Length)
        {
            var read = await socket.ReceiveAsync(buffer.AsMemory(filled), TestContext.CancellationToken);
            Assert.AreNotEqual(0, read, "The connection ended early.");
            filled += read;
        }
    }
}
