using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// <see cref="StreamConnection.PeekByteAsync"/> (ADR-0113, decision 6; BL-734) over an in-memory
/// duplex pipe: the peeked byte stays for the next read or TLS handshake, and waiting runs on
/// the injected clock, so no socket is opened.
/// </summary>
[TestClass]
public sealed class StreamConnectionPeekTests
{
    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 4222);
    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 50000);
    // Long enough that a loaded machine never times out a peek whose byte is on its way; a peek
    // returns as soon as the byte arrives, and the tests that time out run on a manual clock.
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PeekByteAsync_ByteArrived_ReturnsItAndTheNextReadStillReturnsIt()
    {
        var (server, client) = InMemoryDuplexStream.CreatePair();
        await using var connection = new StreamConnection(server, Local, Remote, new DuplexControl(server));
        await client.WriteAsync(Encoding.ASCII.GetBytes("CONNECT"), TestContext.CancellationToken);

        var peeked = await connection.PeekByteAsync(Wait, TestContext.CancellationToken);
        var peekedAgain = await connection.PeekByteAsync(TimeSpan.Zero, TestContext.CancellationToken);
        var first = new byte[16];
        var firstCount = await connection.ReadAsync(first, TestContext.CancellationToken);
        var rest = new byte[16];
        var restCount = await connection.ReadAsync(rest, TestContext.CancellationToken);

        Assert.AreEqual((byte)'C', peeked);
        Assert.AreEqual((byte)'C', peekedAgain);
        Assert.AreEqual("C", Encoding.ASCII.GetString(first, 0, firstCount));
        Assert.AreEqual("ONNECT", Encoding.ASCII.GetString(rest, 0, restCount));
    }

    [TestMethod]
    public async Task PeekByteAsync_NothingSentWithinTheWait_ReturnsNullOnTheInjectedClock()
    {
        var time = new ManualTimeProvider();
        var (server, _) = InMemoryDuplexStream.CreatePair();
        var connection = new StreamConnection(server, Local, Remote, new DuplexControl(server), timeProvider: time);

        var peek = connection.PeekByteAsync(Wait, TestContext.CancellationToken).AsTask();
        await time.FirstTimerCreated;
        time.Advance(Wait - TimeSpan.FromTicks(1));
        await Task.Delay(50, TestContext.CancellationToken);
        Assert.IsFalse(peek.IsCompleted);

        time.Advance(TimeSpan.FromTicks(1));

        Assert.IsNull(await peek);

        // The manual clock never ends a lingering close, so close with a reset instead.
        connection.Abort();
        await connection.DisposeAsync();
    }

    [TestMethod]
    public async Task PeekByteAsync_ByteArrivesAfterTheWait_KeepsItForTheNextRead()
    {
        var (server, client) = InMemoryDuplexStream.CreatePair();
        await using var connection = new StreamConnection(server, Local, Remote, new DuplexControl(server));

        var peeked = await connection.PeekByteAsync(TimeSpan.Zero, TestContext.CancellationToken);
        await client.WriteAsync(Encoding.ASCII.GetBytes("PING"), TestContext.CancellationToken);
        var buffer = new byte[16];
        var count = await connection.ReadAsync(buffer, TestContext.CancellationToken);

        Assert.IsNull(peeked);
        Assert.AreEqual("P", Encoding.ASCII.GetString(buffer, 0, count));
    }

    [TestMethod]
    public async Task PeekByteAsync_PeerHalfClosed_ReturnsNullAndReadsReturnZero()
    {
        var (server, client) = InMemoryDuplexStream.CreatePair();
        await using var connection = new StreamConnection(server, Local, Remote, new DuplexControl(server));
        client.CompleteWrites();

        var peeked = await connection.PeekByteAsync(Wait, TestContext.CancellationToken);
        var first = await connection.ReadAsync(new byte[1], TestContext.CancellationToken);
        var second = await connection.ReadAsync(new byte[1], TestContext.CancellationToken);

        Assert.IsNull(peeked);
        Assert.AreEqual(0, first);
        Assert.AreEqual(0, second);
    }

    [TestMethod]
    public async Task ReadAsync_CancelledWhileAPeeksReadWaits_ThrowsAndTheByteStillArrives()
    {
        var (server, client) = InMemoryDuplexStream.CreatePair();
        await using var connection = new StreamConnection(server, Local, Remote, new DuplexControl(server));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        await connection.PeekByteAsync(TimeSpan.Zero, TestContext.CancellationToken);

        var read = connection.ReadAsync(new byte[1], cancellation.Token).AsTask();
        await cancellation.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => read);
        await client.WriteAsync(new byte[] { 0x2B }, TestContext.CancellationToken);

        Assert.AreEqual((byte)0x2B, await connection.PeekByteAsync(Wait, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task PeekByteAsync_NegativeWait_ThrowsArgumentOutOfRangeException()
    {
        var (server, _) = InMemoryDuplexStream.CreatePair();
        await using var connection = new StreamConnection(server, Local, Remote, new DuplexControl(server));

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            () => connection.PeekByteAsync(TimeSpan.FromTicks(-1), TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task PeekByteAsync_CancelledWhileWaiting_ThrowsOperationCanceledException()
    {
        var (server, _) = InMemoryDuplexStream.CreatePair();
        await using var connection = new StreamConnection(server, Local, Remote, new DuplexControl(server));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        var peek = connection.PeekByteAsync(TimeSpan.FromMinutes(1), cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => peek);
    }

    [TestMethod]
    public async Task PeekByteAsync_AbortedWhileWaiting_ThrowsIOException()
    {
        var (server, _) = InMemoryDuplexStream.CreatePair();
        await using var connection = new StreamConnection(server, Local, Remote, new DuplexControl(server));

        var peek = connection.PeekByteAsync(TimeSpan.FromMinutes(1), TestContext.CancellationToken).AsTask();
        connection.Abort();

        await Assert.ThrowsExactlyAsync<IOException>(() => peek);
    }

    [TestMethod]
    public async Task PeekByteAsync_AfterAbort_ThrowsIOException()
    {
        var (server, _) = InMemoryDuplexStream.CreatePair();
        await using var connection = new StreamConnection(server, Local, Remote, new DuplexControl(server));
        connection.Abort();

        await Assert.ThrowsExactlyAsync<IOException>(
            () => connection.PeekByteAsync(Wait, TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task DisposeAsync_PeeksReadStillWaiting_StopsAtTheLingeringCloseTime()
    {
        var time = new ManualTimeProvider();
        var (server, _) = InMemoryDuplexStream.CreatePair();
        var connection = new StreamConnection(server, Local, Remote, new DuplexControl(server), timeProvider: time);
        var peek = connection.PeekByteAsync(Wait, TestContext.CancellationToken).AsTask();
        await time.FirstTimerCreated;
        time.Advance(Wait);
        Assert.IsNull(await peek);

        var disposing = connection.DisposeAsync().AsTask();
        await time.TimersCreated(3);
        time.Advance(StreamConnection.LingeringCloseTime);
        await disposing;

        Assert.AreEqual(1, server.DisposeCount);
    }

    [TestMethod]
    public async Task DisposeAsync_PeekedByteAndMoreUnread_DiscardsUntilThePeerHalfCloses()
    {
        var (server, client) = InMemoryDuplexStream.CreatePair();
        var control = new DuplexControl(server);
        var connection = new StreamConnection(server, Local, Remote, control);
        await client.WriteAsync(Encoding.ASCII.GetBytes("SUB x 1\r\n"), TestContext.CancellationToken);
        client.CompleteWrites();
        await connection.PeekByteAsync(Wait, TestContext.CancellationToken);

        await connection.DisposeAsync();

        Assert.AreEqual(1, control.ShutdownSendCount);
        Assert.AreEqual(1, server.DisposeCount);
    }

    [TestMethod]
    public async Task DisposeAsync_PeekSawTheHalfClose_ClosesWithoutReadingAgain()
    {
        var (server, client) = InMemoryDuplexStream.CreatePair();
        var control = new DuplexControl(server);
        var connection = new StreamConnection(server, Local, Remote, control);
        client.CompleteWrites();
        await connection.PeekByteAsync(Wait, TestContext.CancellationToken);

        await connection.DisposeAsync();

        Assert.AreEqual(1, control.ShutdownSendCount);
        Assert.AreEqual(1, server.DisposeCount);
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_AfterPeekingTheClientHellosFirstByte_CompletesTheHandshake()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], new FixedTimeProvider(TestCertificates.Now));
        var (server, clientStream) = InMemoryDuplexStream.CreatePair();
        await using var connection = new StreamConnection(
            server, Local, Remote, new DuplexControl(server), ServerTlsHandshake.ForListener(settings, new ListenUrl("natss", "127.0.0.1", 4222)));
        await using var client = new SslStream(clientStream, leaveInnerStreamOpen: true);

        var clientHandshake = client.AuthenticateAsClientAsync(ClientOptions(), TestContext.CancellationToken);
        var peeked = await connection.PeekByteAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        var session = await connection.UpgradeToTlsAsync(TestContext.CancellationToken);
        await clientHandshake;
        await client.WriteAsync(Encoding.ASCII.GetBytes("CONNECT {}\r\n"), TestContext.CancellationToken);
        var buffer = new byte[64];
        var count = await connection.ReadAsync(buffer, TestContext.CancellationToken);

        Assert.AreEqual((byte)0x16, peeked);
        Assert.AreSame(session, connection.TlsSession);
        Assert.AreEqual("CONNECT {}\r\n", Encoding.ASCII.GetString(buffer, 0, count));
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_PeekSawTheHalfClose_ThrowsTlsHandshakeException()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], new FixedTimeProvider(TestCertificates.Now));
        var (server, client) = InMemoryDuplexStream.CreatePair();
        await using var connection = new StreamConnection(
            server, Local, Remote, new DuplexControl(server), ServerTlsHandshake.ForListener(settings, new ListenUrl("natss", "127.0.0.1", 4222)));
        client.CompleteWrites();
        Assert.IsNull(await connection.PeekByteAsync(Wait, TestContext.CancellationToken));

        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(
            () => connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_WhileAPeeksReadWaits_ThrowsInvalidOperationException()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], new FixedTimeProvider(TestCertificates.Now));
        var (server, _) = InMemoryDuplexStream.CreatePair();
        await using var connection = new StreamConnection(
            server, Local, Remote, new DuplexControl(server), ServerTlsHandshake.ForListener(settings, new ListenUrl("natss", "127.0.0.1", 4222)));
        Assert.IsNull(await connection.PeekByteAsync(TimeSpan.Zero, TestContext.CancellationToken));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask());
    }

    private static SslClientAuthenticationOptions ClientOptions() =>
        new()
        {
            TargetHost = "localhost",
            RemoteCertificateValidationCallback = (_, _, _, _) => true,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
        };

    private sealed class DuplexControl(InMemoryDuplexStream stream) : IConnectionTransportControl
    {
        public int ShutdownSendCount { get; private set; }

        public void ShutdownSend()
        {
            ShutdownSendCount++;
            stream.CompleteWrites();
        }

        public void ResetAndClose() => stream.Dispose();
    }
}
