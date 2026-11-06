namespace Surl.Protocol.Ssh;

/// <summary>
/// Reads an SCP exchange's channel data as the protocol comes: single acknowledgement bytes,
/// control lines ending at LF, and a file's bytes. Channel data arrives in chunks of any size, so
/// what one read brings past the byte, line or count asked for is kept for the next.
/// </summary>
/// <param name="channel">The channel's data.</param>
internal sealed class ScpChannelReader(ISshChannelDataStream channel)
{
    private const int BufferBytes = 32768;

    private readonly byte[] buffer = new byte[BufferBytes];
    private int start;
    private int end;

    /// <summary>
    /// Reads one byte.
    /// </summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>The byte, or -1 once the client sent <c>EOF</c> or closed the channel.</returns>
    public async ValueTask<int> ReadByteAsync(CancellationToken cancellationToken) =>
        await FillAsync(cancellationToken) ? buffer[start++] : -1;

    /// <summary>
    /// Reads at most <paramref name="destination"/>'s length of bytes.
    /// </summary>
    /// <param name="destination">Where the bytes go.</param>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>How many bytes were read; 0 once the client sent <c>EOF</c> or closed the channel.</returns>
    public async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        if (!await FillAsync(cancellationToken))
        {
            return 0;
        }

        var count = Math.Min(destination.Length, end - start);
        buffer.AsMemory(start, count).CopyTo(destination);
        start += count;

        return count;
    }

    /// <summary>
    /// Reads one control line, up to and without its LF.
    /// </summary>
    /// <param name="maxLineBytes">The most bytes the line may hold with its LF (<c>--max-line</c>); 0 means no limit.</param>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>
    /// The line, or <see langword="null"/> when the client ended its data first; <c>TooLong</c> is
    /// set, and the line <see langword="null"/>, once the line passes <paramref name="maxLineBytes"/>.
    /// </returns>
    public async ValueTask<(byte[]? Line, bool TooLong)> ReadLineAsync(long maxLineBytes, CancellationToken cancellationToken)
    {
        var line = new List<byte>();
        while (await ReadByteAsync(cancellationToken) is var value and >= 0)
        {
            if (maxLineBytes > 0 && line.Count + 1 > maxLineBytes)
            {
                return (null, true);
            }

            if (value == '\n')
            {
                return ([.. line], false);
            }

            line.Add((byte)value);
        }

        return (null, false);
    }

    private async ValueTask<bool> FillAsync(CancellationToken cancellationToken)
    {
        if (start < end)
        {
            return true;
        }

        start = 0;
        end = await channel.ReadAsync(buffer, cancellationToken);

        return end > 0;
    }
}
