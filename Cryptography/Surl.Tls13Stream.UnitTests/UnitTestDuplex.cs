using System.Threading.Channels;

namespace Surl.Tls13Stream;

/// <summary>
/// One end of an in-memory connection: what one end writes the other reads, and disposing an end
/// ends the other's reads. <see cref="FailWrites"/> makes this end's writes throw, as a connection
/// the peer has dropped does.
/// </summary>
internal sealed class UnitTestDuplex : Stream
{
    private readonly Channel<byte[]> incoming;
    private readonly Channel<byte[]> outgoing;
    private byte[] current = [];
    private int offset;

    private UnitTestDuplex(Channel<byte[]> incoming, Channel<byte[]> outgoing)
    {
        this.incoming = incoming;
        this.outgoing = outgoing;
    }

    public bool FailWrites { get; set; }

    public static (UnitTestDuplex Server, UnitTestDuplex Client) CreatePair()
    {
        var toServer = Channel.CreateUnbounded<byte[]>();
        var toClient = Channel.CreateUnbounded<byte[]>();
        return (new UnitTestDuplex(toServer, toClient), new UnitTestDuplex(toClient, toServer));
    }

    public override bool CanRead => true;

    public override bool CanWrite => true;

    public override bool CanSeek => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (offset == current.Length)
        {
            if (!await incoming.Reader.WaitToReadAsync(cancellationToken))
            {
                return 0;
            }

            current = incoming.Reader.TryRead(out var next) ? next : [];
            offset = 0;
        }

        var count = Math.Min(buffer.Length, current.Length - offset);
        current.AsMemory(offset, count).CopyTo(buffer);
        offset += count;
        return count;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (FailWrites)
        {
            throw new IOException("The peer dropped the connection.");
        }

        outgoing.Writer.TryWrite(buffer.ToArray());
        return ValueTask.CompletedTask;
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        outgoing.Writer.TryComplete();
        base.Dispose(disposing);
    }
}
