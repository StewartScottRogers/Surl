using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Reads bytes from a connection through a buffer, so that several pipelined messages arriving in
/// one read are each taken in turn (RFC 7766 section 6.2.1.1).
/// </summary>
internal sealed class DnsTcpMessageReader(IConnection connection)
{
    private const int ReadSize = 4096;

    private readonly byte[] chunk = new byte[ReadSize];
    private byte[] buffered = [];

    /// <summary>
    /// Waits until at least one byte is buffered: <see langword="false"/> when the client closed
    /// the connection before sending one.
    /// </summary>
    public async Task<bool> WaitForByteAsync(CancellationToken cancellationToken) =>
        buffered.Length > 0 || await FillAsync(cancellationToken);

    /// <summary>
    /// Returns the next <paramref name="count"/> bytes, or <see langword="null"/> when the client
    /// closed the connection before sending them all.
    /// </summary>
    public async Task<byte[]?> ReadExactlyAsync(int count, CancellationToken cancellationToken)
    {
        while (buffered.Length < count)
        {
            if (!await FillAsync(cancellationToken))
            {
                return null;
            }
        }

        var taken = buffered[..count];
        buffered = buffered[count..];
        return taken;
    }

    // Appends one read's bytes to the buffer: false at the end of the stream.
    private async Task<bool> FillAsync(CancellationToken cancellationToken)
    {
        var read = await connection.ReadAsync(chunk, cancellationToken);
        buffered = [.. buffered, .. chunk.AsSpan(0, read)];
        return read > 0;
    }
}
