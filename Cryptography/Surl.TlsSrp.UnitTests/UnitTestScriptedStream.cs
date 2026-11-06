namespace Surl.TlsSrp;

/// <summary>
/// A connection whose client side is scripted: reads return the given client bytes, at most
/// <c>chunkLength</c> at a time, then the end of the stream; writes are kept in
/// <see cref="Written"/>, or throw <see cref="IOException"/> once <see cref="FailWrites"/> is set.
/// </summary>
internal sealed class UnitTestScriptedStream(byte[] clientBytes, int chunkLength = int.MaxValue) : Stream
{
    private int position;

    public MemoryStream Written { get; } = new();

    public bool FailWrites { get; set; }

    public bool IsDisposed { get; private set; }

    public override bool CanRead => true;

    public override bool CanWrite => true;

    public override bool CanSeek => false;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var taken = Math.Min(Math.Min(count, chunkLength), clientBytes.Length - position);
        clientBytes.AsSpan(position, taken).CopyTo(buffer.AsSpan(offset));
        position += taken;
        return taken;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (FailWrites)
        {
            throw new IOException("The client hung up.");
        }

        Written.Write(buffer, offset, count);
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }
}
