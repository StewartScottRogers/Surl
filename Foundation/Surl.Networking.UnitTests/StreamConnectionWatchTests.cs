using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// <see cref="StreamConnection.WatchForPeerCloseAsync"/> (ADR-0141, amendment 2, decision 2; BL-1530)
/// over a scripted stream: bytes then FIN, bytes then reset, a client that stays, and the bytes
/// read ahead reaching the next read or the TLS handshake in order.
/// </summary>
[TestClass]
public sealed class StreamConnectionWatchTests
{
    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 4222);
    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 50000);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WatchForPeerCloseAsync_BytesThenFin_ReportsPeerClosed_AndEveryByteIsRead()
    {
        var script = new ScriptedStream();
        await using var connection = NewConnection(script);
        script.Send("GET /\r\n");
        script.Finish();

        var outcome = await connection.WatchForPeerCloseAsync(64, TestContext.CancellationToken);

        Assert.AreEqual(PeerWatchOutcome.PeerClosed, outcome);
        Assert.AreEqual("GET /\r\n", await ReadAllAsync(connection, 2));
    }

    [TestMethod]
    public async Task WatchForPeerCloseAsync_BytesThenReset_ReportsPeerReset_AndTheNextReadsReturnTheBytesThenThrow()
    {
        var script = new ScriptedStream();
        await using var connection = NewConnection(script);
        script.Send("QUIT\r\n");
        script.Reset();

        var outcome = await connection.WatchForPeerCloseAsync(64, TestContext.CancellationToken);
        var buffer = new byte[16];
        var count = await connection.ReadAsync(buffer, TestContext.CancellationToken);

        Assert.AreEqual(PeerWatchOutcome.PeerReset, outcome);
        Assert.AreEqual("QUIT\r\n", Encoding.ASCII.GetString(buffer, 0, count));
        await Assert.ThrowsExactlyAsync<IOException>(() => connection.ReadAsync(buffer, TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task WatchForPeerCloseAsync_BufferFills_ReportsBufferFull_AndNoByteIsLost()
    {
        var script = new ScriptedStream();
        await using var connection = NewConnection(script);
        script.Send("0123456789");
        script.Finish();

        var outcome = await connection.WatchForPeerCloseAsync(4, TestContext.CancellationToken);

        Assert.AreEqual(PeerWatchOutcome.BufferFull, outcome);
        Assert.AreEqual("0123456789", await ReadAllAsync(connection, 16));
    }

    [TestMethod]
    public async Task WatchForPeerCloseAsync_MoreReadAheadThanTheLimit_ReportsBufferFullAtOnce()
    {
        var script = new ScriptedStream();
        await using var connection = NewConnection(script);
        script.Send("abcdef");
        Assert.AreEqual(PeerWatchOutcome.BufferFull, await connection.WatchForPeerCloseAsync(6, TestContext.CancellationToken));

        var outcome = await connection.WatchForPeerCloseAsync(3, TestContext.CancellationToken);
        script.Finish();

        Assert.AreEqual(PeerWatchOutcome.BufferFull, outcome);
        Assert.AreEqual("abcdef", await ReadAllAsync(connection, 16));
    }

    [TestMethod]
    public async Task WatchForPeerCloseAsync_ClientStaysAndTheWatchIsCancelled_LosesNoByteSentBeforeOrAfter()
    {
        var script = new ScriptedStream();
        await using var connection = NewConnection(script);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        script.Send("ab");

        var watch = connection.WatchForPeerCloseAsync(64, cancellation.Token).AsTask();
        await script.NextReadWaiting();
        await cancellation.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => watch);
        script.Send("cd");
        script.Finish();

        Assert.AreEqual("abcd", await ReadAllAsync(connection, 1));
    }

    [TestMethod]
    public async Task WatchForPeerCloseAsync_AfterAPeekThatFoundNothing_WaitsForThatReadAndKeepsItsByteFirst()
    {
        var script = new ScriptedStream();
        await using var connection = NewConnection(script);
        Assert.IsNull(await connection.PeekByteAsync(TimeSpan.Zero, TestContext.CancellationToken));
        script.Send("xyz");
        script.Finish();

        var outcome = await connection.WatchForPeerCloseAsync(64, TestContext.CancellationToken);

        Assert.AreEqual(PeerWatchOutcome.PeerClosed, outcome);
        Assert.AreEqual("xyz", await ReadAllAsync(connection, 16));
    }

    [TestMethod]
    public async Task PeekByteAsync_AfterAWatchReadAhead_ReturnsTheFirstByteAndKeepsTheRest()
    {
        var script = new ScriptedStream();
        await using var connection = NewConnection(script);
        script.Send("xyz");
        Assert.AreEqual(PeerWatchOutcome.BufferFull, await connection.WatchForPeerCloseAsync(3, TestContext.CancellationToken));
        script.Finish();

        Assert.AreEqual((byte)'x', await connection.PeekByteAsync(TimeSpan.Zero, TestContext.CancellationToken));
        Assert.AreEqual("xyz", await ReadAllAsync(connection, 16));
    }

    [TestMethod]
    public async Task WatchForPeerCloseAsync_ResetBeforeAnyByte_ReportsPeerReset_AndDisposeStillCloses()
    {
        var script = new ScriptedStream();
        var connection = NewConnection(script);
        script.Reset();

        Assert.AreEqual(PeerWatchOutcome.PeerReset, await connection.WatchForPeerCloseAsync(64, TestContext.CancellationToken));
        await connection.DisposeAsync();

        Assert.AreEqual(1, script.DisposeCount);
    }

    [TestMethod]
    public async Task WatchForPeerCloseAsync_NonPositiveLimit_ThrowsArgumentOutOfRangeException()
    {
        await using var connection = NewConnection(new ScriptedStream());

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            () => connection.WatchForPeerCloseAsync(0, TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task WatchForPeerCloseAsync_AfterAbort_ThrowsIOException()
    {
        var connection = NewConnection(new ScriptedStream());
        connection.Abort();

        await Assert.ThrowsExactlyAsync<IOException>(
            () => connection.WatchForPeerCloseAsync(64, TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_AfterAWatchReadAheadTheClientHello_CompletesTheHandshake()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], new FixedTimeProvider(TestCertificates.Now));
        var (server, clientStream) = InMemoryDuplexStream.CreatePair();
        await using var connection = new StreamConnection(
            server, Local, Remote, new DuplexControl(server), ServerTlsHandshake.ForListener(settings, new ListenUrl("natss", "127.0.0.1", 4222)));
        await using var client = new SslStream(clientStream, leaveInnerStreamOpen: true);

        var clientHandshake = client.AuthenticateAsClientAsync(ClientOptions(), TestContext.CancellationToken);
        var outcome = await connection.WatchForPeerCloseAsync(5, TestContext.CancellationToken);
        var session = await connection.UpgradeToTlsAsync(TestContext.CancellationToken);
        await clientHandshake;
        await client.WriteAsync(Encoding.ASCII.GetBytes("PING\r\n"), TestContext.CancellationToken);
        var buffer = new byte[16];
        var count = await connection.ReadAsync(buffer, TestContext.CancellationToken);

        Assert.AreEqual(PeerWatchOutcome.BufferFull, outcome);
        Assert.AreSame(session, connection.TlsSession);
        Assert.AreEqual("PING\r\n", Encoding.ASCII.GetString(buffer, 0, count));
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_WhileAWatchWaits_ThrowsInvalidOperationException()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], new FixedTimeProvider(TestCertificates.Now));
        var script = new ScriptedStream();
        var connection = new StreamConnection(
            script, Local, Remote, new ScriptedControl(), ServerTlsHandshake.ForListener(settings, new ListenUrl("natss", "127.0.0.1", 4222)));
        var watch = connection.WatchForPeerCloseAsync(64, TestContext.CancellationToken).AsTask();
        await script.NextReadWaiting();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask());

        script.Finish();
        Assert.AreEqual(PeerWatchOutcome.PeerClosed, await watch);
        await connection.DisposeAsync();
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_APeeksReadFinishedWithBytesNobodyTook_HandsThemToTheHandshake()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], new FixedTimeProvider(TestCertificates.Now));
        var script = new ScriptedStream();
        await using var connection = new StreamConnection(
            script, Local, Remote, new ScriptedControl(), ServerTlsHandshake.ForListener(settings, new ListenUrl("natss", "127.0.0.1", 4222)));
        Assert.IsNull(await connection.PeekByteAsync(TimeSpan.Zero, TestContext.CancellationToken));
        script.Send("x");
        script.Finish();
        await script.NextReadReturned();

        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(
            () => connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask());
    }

    private static StreamConnection NewConnection(ScriptedStream script) => new(script, Local, Remote, new ScriptedControl());

    // Reads until the connection returns 0, with a buffer of the given size, so partial reads are exercised.
    private async Task<string> ReadAllAsync(StreamConnection connection, int bufferSize)
    {
        var all = new StringBuilder();
        var buffer = new byte[bufferSize];

        while (true)
        {
            var count = await connection.ReadAsync(buffer, TestContext.CancellationToken);

            if (count == 0)
            {
                return all.ToString();
            }

            all.Append(Encoding.ASCII.GetString(buffer, 0, count));
        }
    }

    private static SslClientAuthenticationOptions ClientOptions() =>
        new()
        {
            TargetHost = "localhost",
            RemoteCertificateValidationCallback = (_, _, _, _) => true,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
        };

    private sealed class ScriptedControl : IConnectionTransportControl
    {
        public void ShutdownSend()
        {
        }

        public void ResetAndClose()
        {
        }
    }

    private sealed class DuplexControl(InMemoryDuplexStream stream) : IConnectionTransportControl
    {
        public void ShutdownSend() => stream.CompleteWrites();

        public void ResetAndClose() => stream.Dispose();
    }

    // A stream whose reads follow a script: bytes (handed out in as many reads as the caller's buffers need),
    // then a FIN (reads return 0 from then on) or a reset (reads throw IOException from then on).
    private sealed class ScriptedStream : Stream
    {
        private readonly Channel<(byte[] Bytes, bool Reset)> script = Channel.CreateUnbounded<(byte[], bool)>();
        private readonly SemaphoreSlim readWaiting = new(0);
        private readonly SemaphoreSlim readReturned = new(0);
        private byte[] unread = [];
        private int unreadOffset;
        private bool finished;
        private bool reset;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public int DisposeCount { get; private set; }

        public void Send(string text) => script.Writer.TryWrite((Encoding.ASCII.GetBytes(text), false));

        public void Finish() => script.Writer.TryWrite(([], false));

        public void Reset() => script.Writer.TryWrite(([], true));

        public Task NextReadWaiting() => readWaiting.WaitAsync();

        public Task NextReadReturned() => readReturned.WaitAsync();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            while (unreadOffset == unread.Length && !finished && !reset)
            {
                readWaiting.Release();

                var (bytes, resets) = await script.Reader.ReadAsync(cancellationToken);

                unread = bytes;
                unreadOffset = 0;
                reset = resets;
                finished = bytes.Length == 0 && !resets;
            }

            if (reset)
            {
                throw new IOException("The scripted peer reset the connection.");
            }

            var count = Math.Min(buffer.Length, unread.Length - unreadOffset);

            unread.AsMemory(unreadOffset, count).CopyTo(buffer);
            unreadOffset += count;
            readReturned.Release();

            return count;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing) => DisposeCount++;
    }
}
