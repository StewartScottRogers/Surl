using System.Text;
using System.Threading.Channels;

namespace Surl.McpServing;

/// <summary>
/// An in-memory stand-in for a server's standard output: keeps every write as it arrived, hands each one to
/// <see cref="NextWriteAsync" /> in order, and throws <see cref="IOException" /> for every write after the
/// first <c>writesBeforeFailure</c>, as a closed pipe does.
/// </summary>
internal sealed class UnitTestLineOutputStream(int writesBeforeFailure = int.MaxValue) : Stream
{
    private readonly Channel<string> _writes = Channel.CreateUnbounded<string>();

    private readonly List<string> _allWrites = [];

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public TaskCompletionSource WriteFailed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IReadOnlyList<string> Writes
    {
        get
        {
            lock (_allWrites)
            {
                return [.. _allWrites];
            }
        }
    }

    public async Task<string> NextWriteAsync() =>
        await _writes.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        string text = Encoding.UTF8.GetString(buffer.Span);
        lock (_allWrites)
        {
            if (_allWrites.Count >= writesBeforeFailure)
            {
                WriteFailed.TrySetResult();
                throw new IOException("The pipe is closed.");
            }

            _allWrites.Add(text);
        }

        _writes.Writer.TryWrite(text);
        return ValueTask.CompletedTask;
    }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override void Flush() => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
