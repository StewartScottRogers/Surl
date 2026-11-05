using System.Text;
using System.Threading.Channels;

namespace Surl.McpServing;

/// <summary>
/// An in-memory stand-in for a server's standard input: each <see cref="WriteLine" /> is one chunk a read
/// returns, and <see cref="Complete" /> ends the input as a client closing it does.
/// </summary>
internal sealed class UnitTestChannelInputStream : Stream
{
    private readonly Channel<byte[]> _chunks = Channel.CreateUnbounded<byte[]>();

    private byte[] _current = [];

    private int _offset;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public void WriteLine(string json) => _chunks.Writer.TryWrite(Encoding.UTF8.GetBytes(json + "\n"));

    public void Complete() => _chunks.Writer.TryComplete();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_offset == _current.Length)
        {
            if (!await _chunks.Reader.WaitToReadAsync(cancellationToken))
            {
                return 0;
            }

            _current = await _chunks.Reader.ReadAsync(cancellationToken);
            _offset = 0;
        }

        int count = Math.Min(buffer.Length, _current.Length - _offset);
        _current.AsSpan(_offset, count).CopyTo(buffer.Span);
        _offset += count;
        return count;
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Flush() => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
