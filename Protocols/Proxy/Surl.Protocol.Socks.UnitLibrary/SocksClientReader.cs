using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// Reads curl's connection a byte at a time for the SOCKS handshake, holding any bytes that
/// arrived after the request so the relay forwards them to the origin first.
/// </summary>
/// <param name="connection">curl's connection.</param>
internal sealed class SocksClientReader(IConnection connection)
{
    private readonly byte[] buffer = new byte[4096];
    private int start;
    private int end;

    /// <summary>
    /// Reads the next byte.
    /// </summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The byte, or -1 once curl has half-closed and every byte before it has been read.</returns>
    public async ValueTask<int> ReadByteAsync(CancellationToken cancellationToken)
    {
        if (start == end)
        {
            start = 0;
            end = await connection.ReadAsync(buffer, cancellationToken);
            if (end == 0)
            {
                return -1;
            }
        }

        return buffer[start++];
    }

    /// <summary>
    /// Reads exactly <paramref name="count"/> bytes.
    /// </summary>
    /// <param name="count">How many bytes to read, 0 to 255.</param>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The bytes, or <see langword="null"/> when curl half-closed before the last of them.</returns>
    public async ValueTask<byte[]?> ReadBytesAsync(int count, CancellationToken cancellationToken)
    {
        var bytes = new byte[count];
        for (var index = 0; index < count; index++)
        {
            var next = await ReadByteAsync(cancellationToken);
            if (next < 0)
            {
                return null;
            }

            bytes[index] = (byte)next;
        }

        return bytes;
    }

    /// <summary>
    /// Reads a field SOCKS5 and RFC 1929 send as one length byte and that many bytes.
    /// </summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The field's bytes, or <see langword="null"/> when curl half-closed before its end.</returns>
    public async ValueTask<byte[]?> ReadLengthPrefixedAsync(CancellationToken cancellationToken)
    {
        var length = await ReadByteAsync(cancellationToken);

        return length < 0 ? null : await ReadBytesAsync(length, cancellationToken);
    }

    /// <summary>
    /// Copies the bytes held from earlier reads into <paramref name="destination"/>, or, when none
    /// are held, reads the connection.
    /// </summary>
    /// <param name="destination">Where the bytes go; must not be empty.</param>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>How many bytes were copied; 0 once curl has half-closed.</returns>
    public ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        if (start == end)
        {
            return connection.ReadAsync(destination, cancellationToken);
        }

        var count = Math.Min(destination.Length, end - start);
        buffer.AsMemory(start, count).CopyTo(destination);
        start += count;

        return ValueTask.FromResult(count);
    }
}
