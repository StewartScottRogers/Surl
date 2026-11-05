using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// The connection's bytes as the <see cref="System.Xml.XmlReader"/> reads them (ADR-0114
/// decisions 6 and 7): checked to be UTF-8, counted against the size bound since the last
/// <see cref="StartElementCount"/>, and the most recent kept so a parse failure can be told apart.
/// </summary>
/// <remarks>
/// The reader asks for at most <see cref="MaxReadBytes"/> at a time, and the count starts when a
/// first-level element's start tag has been read, so a stanza is measured to within one read of its
/// size, never more than it is: bytes of it the reader had buffered before its start tag was reported
/// are not counted.
/// </remarks>
/// <param name="connection">The connection read.</param>
/// <param name="maxElementBytes">The size bound, <c>--max-message</c>.</param>
internal sealed class XmppInputStream(IConnection connection, long maxElementBytes) : Stream
{
    /// <summary>The most one read hands the XML reader.</summary>
    public const int MaxReadBytes = 4096;

    /// <summary>How many of the most recent bytes are kept for <see cref="RecentText"/>.</summary>
    public const int RecentBytes = 16384;

    private readonly Decoder utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetDecoder();
    private readonly Queue<byte> recent = new();
    private long elementBytes;

    /// <summary>Cuts the next read off; set before every read of the XML reader.</summary>
    public CancellationToken ReadCancellation { get; set; }

    /// <summary>Whether the client has half-closed and every byte before it has been read.</summary>
    public bool ReachedEnd { get; private set; }

    /// <summary>The most recent bytes read, up to <see cref="RecentBytes"/>, as UTF-8 text.</summary>
    public string RecentText => Encoding.UTF8.GetString(recent.ToArray());

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>Starts counting a new element's bytes against the size bound.</summary>
    public void StartElementCount() => elementBytes = 0;

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var count = await connection.ReadAsync(buffer[..Math.Min(buffer.Length, MaxReadBytes)], ReadCancellation);
        if (count == 0)
        {
            ReachedEnd = true;
            return 0;
        }

        Accept(buffer.Span[..count]);
        return count;
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Flush() => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private void Accept(ReadOnlySpan<byte> bytes)
    {
        try
        {
            utf8.GetCharCount(bytes, flush: false);
        }
        catch (DecoderFallbackException)
        {
            throw new XmppStreamErrorException(XmppStreamErrors.UnsupportedEncoding, "The client sent bytes that are not UTF-8");
        }

        foreach (var value in bytes)
        {
            recent.Enqueue(value);
        }

        while (recent.Count > RecentBytes)
        {
            recent.Dequeue();
        }

        elementBytes += bytes.Length;
        if (elementBytes > maxElementBytes)
        {
            throw new XmppStreamErrorException(
                XmppStreamErrors.PolicyViolation, $"An element passed the size bound of {maxElementBytes} bytes");
        }
    }
}
