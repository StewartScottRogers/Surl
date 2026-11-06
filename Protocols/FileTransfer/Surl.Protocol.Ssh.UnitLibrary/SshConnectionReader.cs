using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Buffers what the client sends, so the identification line can be read a byte at a time
/// and each packet a field at a time, from one buffer.
/// </summary>
/// <param name="connection">The connection to read from.</param>
internal sealed class SshConnectionReader(IConnection connection)
{
    /// <summary>
    /// How much of a long read is allocated before any of it arrives. The array doubles as
    /// bytes come, so a peer that announces a large packet and sends nothing costs no more.
    /// </summary>
    private const int InitialReadBytes = 65536;

    private readonly byte[] buffer = new byte[4096];

    private int start;

    private int end;

    /// <summary>
    /// Reads one byte.
    /// </summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The byte, or -1 once the client has closed the connection.</returns>
    public async ValueTask<int> ReadByteAsync(CancellationToken cancellationToken) =>
        start == end && !await FillAsync(cancellationToken) ? -1 : buffer[start++];

    /// <summary>
    /// Reads exactly <paramref name="count"/> bytes.
    /// </summary>
    /// <param name="count">How many bytes to read.</param>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The bytes, or <see langword="null"/> when the client closed the connection first.</returns>
    public async ValueTask<byte[]?> ReadExactlyAsync(int count, CancellationToken cancellationToken)
    {
        var bytes = new byte[Math.Min(count, InitialReadBytes)];
        var filled = 0;
        while (filled < count)
        {
            if (start == end && !await FillAsync(cancellationToken))
            {
                return null;
            }

            if (filled == bytes.Length)
            {
                Array.Resize(ref bytes, (int)Math.Min(count, 2L * bytes.Length));
            }

            var taken = Math.Min(bytes.Length - filled, end - start);
            buffer.AsSpan(start, taken).CopyTo(bytes.AsSpan(filled));
            start += taken;
            filled += taken;
        }

        return bytes;
    }

    private async ValueTask<bool> FillAsync(CancellationToken cancellationToken)
    {
        start = 0;
        end = await connection.ReadAsync(buffer, cancellationToken);

        return end > 0;
    }
}
