using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// Reads the client's control lines (ADR-0113 decision 0): each ends with CR LF or a bare LF,
/// however its bytes are split across reads, and none may be longer than the line limit, its
/// ending included.
/// </summary>
/// <param name="connection">The connection read from.</param>
/// <param name="maxLineBytes">The longest line, its ending included (<c>--max-line</c>).</param>
internal sealed class NatsControlLineReader(IConnection connection, long maxLineBytes)
{
    private const int InitialBufferBytes = 4096;

    private byte[] buffer = new byte[InitialBufferBytes];
    private int start;
    private int end;

    /// <summary>
    /// Reads the next line, without its ending; null once the client has closed its side (a
    /// part line it left is dropped).
    /// </summary>
    /// <exception cref="NatsProtocolViolationException">The line is over the limit.</exception>
    public async ValueTask<byte[]?> ReadLineAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = TryTakeLine();
            if (line is not null)
            {
                return line;
            }

            if (!await FillAsync(cancellationToken))
            {
                return null;
            }
        }
    }

    private byte[]? TryTakeLine()
    {
        var pending = buffer.AsSpan(start, end - start);
        var lineFeed = pending.IndexOf((byte)'\n');
        if (lineFeed < 0)
        {
            // Without its line feed yet, the line is already at least one byte longer than this.
            ThrowIfOverLimit(pending.Length + 1);
            return null;
        }

        ThrowIfOverLimit(lineFeed + 1);
        start += lineFeed + 1;
        var content = pending[..lineFeed];
        return (content.EndsWith((byte)'\r') ? content[..^1] : content).ToArray();
    }

    private void ThrowIfOverLimit(long lineBytes)
    {
        if (lineBytes > maxLineBytes)
        {
            throw new NatsProtocolViolationException(
                NatsErrors.MaximumControlLineExceeded,
                $"A control line was longer than {maxLineBytes} bytes");
        }
    }

    private async ValueTask<bool> FillAsync(CancellationToken cancellationToken)
    {
        Array.Copy(buffer, start, buffer, 0, end - start);
        end -= start;
        start = 0;
        if (end == buffer.Length)
        {
            Array.Resize(ref buffer, buffer.Length * 2);
        }

        var read = await connection.ReadAsync(buffer.AsMemory(end), cancellationToken);
        end += read;
        return read > 0;
    }
}
