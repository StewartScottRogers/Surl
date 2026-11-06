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
