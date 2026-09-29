using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Surl.Networking;

[TestClass]
public sealed class StreamConnectionTests
{
    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 8080);
    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 50000);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_KeepsTheEndPoints()
    {
        var connection = new StreamConnection(new FakeStream(), Local, Remote, new FakeTransportControl());

        Assert.AreSame(Local, connection.LocalEndPoint);
        Assert.AreSame(Remote, connection.RemoteEndPoint);
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var stream = new FakeStream();
        var control = new FakeTransportControl();

        Assert.ThrowsExactly<ArgumentNullException>(() => new StreamConnection(null!, Local, Remote, control));
        Assert.ThrowsExactly<ArgumentNullException>(() => new StreamConnection(stream, null!, Remote, control));
        Assert.ThrowsExactly<ArgumentNullException>(() => new StreamConnection(stream, Local, null!, control));
        Assert.ThrowsExactly<ArgumentNullException>(() => new StreamConnection(stream, Local, Remote, null!));
    }

    [TestMethod]
    public async Task ReadAsync_BytesArrived_CopiesThem()
    {
        var stream = new FakeStream { Inbound = Encoding.ASCII.GetBytes("GET /") };
        var connection = new StreamConnection(stream, Local, Remote, new FakeTransportControl());
        var buffer = new byte[16];

        var count = await connection.ReadAsync(buffer, TestContext.CancellationToken);

        Assert.AreEqual("GET /", Encoding.ASCII.GetString(buffer, 0, count));
    }

    [TestMethod]
    public async Task ReadAsync_PeerHalfClosed_ReturnsZeroOnEveryCall()
    {
        var connection = new StreamConnection(new FakeStream(), Local, Remote, new FakeTransportControl());

        var first = await connection.ReadAsync(new byte[16], TestContext.CancellationToken);
        var second = await connection.ReadAsync(new byte[16], TestContext.CancellationToken);

        Assert.AreEqual(0, first);
        Assert.AreEqual(0, second);
    }

    [TestMethod]
    public async Task ReadAsync_EmptyBuffer_Throws()
    {
        var connection = new StreamConnection(new FakeStream(), Local, Remote, new FakeTransportControl());

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => connection.ReadAsync(Memory<byte>.Empty, TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task ReadAsync_AlreadyCancelled_Throws()
    {
        var connection = new StreamConnection(new FakeStream(), Local, Remote, new FakeTransportControl());

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => connection.ReadAsync(new byte[16], new CancellationToken(true)).AsTask());
    }

    [TestMethod]
    public async Task ReadAsync_AbortedWhileWaiting_ThrowsIOException()
    {
        var stream = new FakeStream { ReadWaits = true };
        var connection = new StreamConnection(stream, Local, Remote, new FakeTransportControl());

        var read = connection.ReadAsync(new byte[16], TestContext.CancellationToken).AsTask();
        connection.Abort();
        stream.FailWaitingCall(new ObjectDisposedException(nameof(FakeStream)));

        await Assert.ThrowsExactlyAsync<IOException>(() => read);
    }

    [TestMethod]
    public async Task ReadAsync_StreamDisposedWithoutAbort_ThrowsObjectDisposedException()
    {
        var stream = new FakeStream { ReadWaits = true };
        var connection = new StreamConnection(stream, Local, Remote, new FakeTransportControl());

        var read = connection.ReadAsync(new byte[16], TestContext.CancellationToken).AsTask();
        stream.FailWaitingCall(new ObjectDisposedException(nameof(FakeStream)));

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => read);
    }

    [TestMethod]
    public async Task ReadAsync_AfterAbort_ThrowsIOException()
    {
        var connection = new StreamConnection(new FakeStream(), Local, Remote, new FakeTransportControl());
        connection.Abort();

        await Assert.ThrowsExactlyAsync<IOException>(
            () => connection.ReadAsync(new byte[16], TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task ReadAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var connection = new StreamConnection(new FakeStream(), Local, Remote, new FakeTransportControl());
        await connection.DisposeAsync();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            () => connection.ReadAsync(new byte[16], TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task WriteAsync_Bytes_HandsThemToTheStream()
    {
        var stream = new FakeStream();
        var connection = new StreamConnection(stream, Local, Remote, new FakeTransportControl());

        await connection.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK"), TestContext.CancellationToken);

        Assert.AreEqual("HTTP/1.1 200 OK", Encoding.ASCII.GetString(stream.Written.ToArray()));
    }

    [TestMethod]
    public async Task WriteAsync_AfterCompleteWrites_ThrowsInvalidOperationException()
    {
        var connection = new StreamConnection(new FakeStream(), Local, Remote, new FakeTransportControl());
        await connection.CompleteWritesAsync(TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => connection.WriteAsync(new byte[1], TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task WriteAsync_AbortedWhileWriting_ThrowsIOException()
    {
        var stream = new FakeStream { WriteWaits = true };
        var connection = new StreamConnection(stream, Local, Remote, new FakeTransportControl());

        var write = connection.WriteAsync(new byte[1], TestContext.CancellationToken).AsTask();
        connection.Abort();
        stream.FailWaitingCall(new ObjectDisposedException(nameof(FakeStream)));

        await Assert.ThrowsExactlyAsync<IOException>(() => write);
    }

    [TestMethod]
    public async Task WriteAsync_StreamDisposedWithoutAbort_ThrowsObjectDisposedException()
    {
        var stream = new FakeStream { WriteWaits = true };
        var connection = new StreamConnection(stream, Local, Remote, new FakeTransportControl());

        var write = connection.WriteAsync(new byte[1], TestContext.CancellationToken).AsTask();
        stream.FailWaitingCall(new ObjectDisposedException(nameof(FakeStream)));

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => write);
    }

    [TestMethod]
    public async Task WriteAsync_AfterAbort_ThrowsIOException()
    {
        var connection = new StreamConnection(new FakeStream(), Local, Remote, new FakeTransportControl());
        connection.Abort();

        await Assert.ThrowsExactlyAsync<IOException>(
            () => connection.WriteAsync(new byte[1], TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task WriteAsync_AlreadyCancelled_Throws()
    {
        var connection = new StreamConnection(new FakeStream(), Local, Remote, new FakeTransportControl());

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => connection.WriteAsync(new byte[1], new CancellationToken(true)).AsTask());
    }

    [TestMethod]
    public async Task CompleteWritesAsync_FlushesThenShutsDownSending()
    {
        var stream = new FakeStream();
        var control = new FakeTransportControl { Stream = stream };
        var connection = new StreamConnection(stream, Local, Remote, control);

        await connection.CompleteWritesAsync(TestContext.CancellationToken);

        Assert.AreEqual(1, control.ShutdownSendCount);
        Assert.AreEqual(1, control.FlushCountAtShutdown);
    }

    [TestMethod]
    public async Task CompleteWritesAsync_Twice_ShutsDownSendingOnce()
    {
        var control = new FakeTransportControl();
        var connection = new StreamConnection(new FakeStream(), Local, Remote, control);

        await connection.CompleteWritesAsync(TestContext.CancellationToken);
        await connection.CompleteWritesAsync(TestContext.CancellationToken);

        Assert.AreEqual(1, control.ShutdownSendCount);
    }

    [TestMethod]
    public async Task CompleteWritesAsync_ShutdownFailsWithASocketError_ThrowsIOException()
    {
        var failure = new SocketException((int)SocketError.ConnectionReset);
        var connection = new StreamConnection(
            new FakeStream(), Local, Remote, new FakeTransportControl { ShutdownSendFailure = failure });

        var exception = await Assert.ThrowsExactlyAsync<IOException>(
            () => connection.CompleteWritesAsync(TestContext.CancellationToken).AsTask());

        Assert.AreSame(failure, exception.InnerException);
    }

    [TestMethod]
    public async Task CompleteWritesAsync_AbortedWhileFlushing_ThrowsIOException()
    {
        var stream = new FakeStream { FlushWaits = true };
        var control = new FakeTransportControl { ShutdownSendFailure = new ObjectDisposedException("Socket") };
        var connection = new StreamConnection(stream, Local, Remote, control);

        var completing = connection.CompleteWritesAsync(TestContext.CancellationToken).AsTask();
        connection.Abort();
        stream.CompleteWaitingCall();

        await Assert.ThrowsExactlyAsync<IOException>(() => completing);
    }

    [TestMethod]
    public async Task CompleteWritesAsync_SocketDisposedWithoutAbort_ThrowsObjectDisposedException()
    {
        var control = new FakeTransportControl { ShutdownSendFailure = new ObjectDisposedException("Socket") };
        var connection = new StreamConnection(new FakeStream(), Local, Remote, control);

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            () => connection.CompleteWritesAsync(TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task CompleteWritesAsync_AfterAbort_ThrowsIOException()
    {
        var connection = new StreamConnection(new FakeStream(), Local, Remote, new FakeTransportControl());
        connection.Abort();

        await Assert.ThrowsExactlyAsync<IOException>(
            () => connection.CompleteWritesAsync(TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public void Abort_Twice_ResetsOnce()
    {
        var control = new FakeTransportControl();
        var connection = new StreamConnection(new FakeStream(), Local, Remote, control);

        connection.Abort();
        connection.Abort();

        Assert.AreEqual(1, control.ResetAndCloseCount);
    }

    [TestMethod]
    public async Task Abort_AfterDispose_DoesNothing()
    {
        var control = new FakeTransportControl();
        var connection = new StreamConnection(new FakeStream(), Local, Remote, control);
        await connection.DisposeAsync();

        connection.Abort();

        Assert.AreEqual(0, control.ResetAndCloseCount);
    }

    [TestMethod]
    public async Task DisposeAsync_WritesNotCompleted_CompletesThemThenDisposesTheStream()
    {
        var stream = new FakeStream();
        var control = new FakeTransportControl();
        var connection = new StreamConnection(stream, Local, Remote, control);

        await connection.DisposeAsync();

        Assert.AreEqual(1, control.ShutdownSendCount);
        Assert.IsTrue(stream.IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsync_WritesAlreadyCompleted_DoesNotShutDownAgain()
    {
        var control = new FakeTransportControl();
        var connection = new StreamConnection(new FakeStream(), Local, Remote, control);
        await connection.CompleteWritesAsync(TestContext.CancellationToken);

        await connection.DisposeAsync();

        Assert.AreEqual(1, control.ShutdownSendCount);
    }

    [TestMethod]
    public async Task DisposeAsync_AfterAbort_DisposesTheStreamWithoutShuttingDown()
    {
        var stream = new FakeStream();
        var control = new FakeTransportControl();
        var connection = new StreamConnection(stream, Local, Remote, control);
        connection.Abort();

        await connection.DisposeAsync();

        Assert.AreEqual(0, control.ShutdownSendCount);
        Assert.IsTrue(stream.IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsync_PeerAlreadyGone_StillDisposesTheStream()
    {
        var stream = new FakeStream();
        var control = new FakeTransportControl
        {
            ShutdownSendFailure = new SocketException((int)SocketError.ConnectionReset),
        };
        var connection = new StreamConnection(stream, Local, Remote, control);

        await connection.DisposeAsync();

        Assert.IsTrue(stream.IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsync_Twice_DisposesTheStreamOnce()
    {
        var stream = new FakeStream();
        var connection = new StreamConnection(stream, Local, Remote, new FakeTransportControl());

        await connection.DisposeAsync();
        await connection.DisposeAsync();

        Assert.AreEqual(1, stream.DisposeCount);
    }

    private sealed class FakeTransportControl : IConnectionTransportControl
    {
        public FakeStream? Stream { get; init; }

        public Exception? ShutdownSendFailure { get; init; }

        public int ShutdownSendCount { get; private set; }

        public int FlushCountAtShutdown { get; private set; }

        public int ResetAndCloseCount { get; private set; }

        public void ShutdownSend()
        {
            if (ShutdownSendFailure is not null)
            {
                throw ShutdownSendFailure;
            }

            ShutdownSendCount++;
            FlushCountAtShutdown = Stream?.FlushCount ?? 0;
        }

        public void ResetAndClose() => ResetAndCloseCount++;
    }

    private sealed class FakeStream : Stream
    {
        private readonly TaskCompletionSource waitingCall = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int inboundOffset;

        public byte[] Inbound { get; init; } = [];

        public bool ReadWaits { get; init; }

        public bool WriteWaits { get; init; }

        public bool FlushWaits { get; init; }

        public MemoryStream Written { get; } = new();

        public int FlushCount { get; private set; }

        public int DisposeCount { get; private set; }

        public bool IsDisposed => DisposeCount > 0;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public void FailWaitingCall(Exception exception) => waitingCall.SetException(exception);

        public void CompleteWaitingCall() => waitingCall.SetResult();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (ReadWaits)
            {
                await waitingCall.Task;
            }

            var count = Math.Min(buffer.Length, Inbound.Length - inboundOffset);
            Inbound.AsMemory(inboundOffset, count).CopyTo(buffer);
            inboundOffset += count;

            return count;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (WriteWaits)
            {
                await waitingCall.Task;
            }

            Written.Write(buffer.Span);
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (FlushWaits)
            {
                await waitingCall.Task;
            }

            FlushCount++;
        }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask DisposeAsync()
        {
            DisposeCount++;

            return ValueTask.CompletedTask;
        }
    }
}
