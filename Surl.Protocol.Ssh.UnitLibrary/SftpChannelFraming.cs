using System.Buffers.Binary;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Frames an SFTP session on a channel's data: each packet is a <c>uint32</c> length and that many
/// bytes (draft-ietf-secsh-filexfer-02, section 3). A length past <c>--max-message</c> with its own
/// four bytes, or below 5, ends the session before the body is read (ADR-0054, decision 5).
/// </summary>
/// <param name="channel">The channel's data both ways.</param>
/// <param name="maxMessageBytes">The most bytes a packet may hold with its length field (<c>--max-message</c>); 0 means no limit.</param>
internal sealed class SftpChannelFraming(ISshChannelDataStream channel, long maxMessageBytes)
{
    private const uint InitialBodyBytes = 65536;

    /// <summary>
    /// Reads the client's next packet.
    /// </summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>The packet after its length field; <see langword="null"/> once the client sent <c>EOF</c> between packets.</returns>
    /// <exception cref="SftpSessionEndedException">
    /// The packet is past the bound, shorter than a type and an id, too large to hold, or cut off by <c>EOF</c>.
    /// </exception>
    public async ValueTask<byte[]?> ReadPacketAsync(CancellationToken cancellationToken)
    {
        var header = new byte[4];
        var read = await FillAsync(header, cancellationToken);
        if (read == 0)
        {
            return null;
        }

        return await ReadBodyAsync(BodyLength(header, read), cancellationToken);
    }

    /// <summary>
    /// Sends a packet built by <see cref="SftpReply"/>.
    /// </summary>
    /// <param name="packet">The framed packet.</param>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>A task that completes when the packet is written.</returns>
    public ValueTask WriteAsync(byte[] packet, CancellationToken cancellationToken) => channel.WriteAsync(packet, cancellationToken);

    // The length field, checked against the bound before anything is allocated for the body.
    private uint BodyLength(byte[] header, int read)
    {
        if (read < header.Length)
        {
            throw new SftpSessionEndedException("malformed packet");
        }

        var length = BinaryPrimitives.ReadUInt32BigEndian(header);
        if (maxMessageBytes > 0 && length + 4L > maxMessageBytes)
        {
            throw new SftpSessionEndedException("packet past --max-message");
        }

        return length < 5 || length > Array.MaxLength
            ? throw new SftpSessionEndedException("malformed packet")
            : length;
    }

    // The buffer grows as the bytes arrive, doubling from 64 KiB, so a length field alone never
    // makes the server hold more than twice what the client actually sent.
    private async ValueTask<byte[]> ReadBodyAsync(uint length, CancellationToken cancellationToken)
    {
        var body = new byte[Math.Min(length, InitialBodyBytes)];
        var filled = await FillAsync(body, cancellationToken);
        while (filled == body.Length && filled < length)
        {
            Array.Resize(ref body, (int)Math.Min(length, body.Length * 2L));
            filled += await FillAsync(body.AsMemory(filled), cancellationToken);
        }

        return filled < length ? throw new SftpSessionEndedException("malformed packet") : body;
    }

    private async ValueTask<int> FillAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var filled = 0;
        while (filled < buffer.Length)
        {
            var read = await channel.ReadAsync(buffer[filled..], cancellationToken);
            if (read == 0)
            {
                break;
            }

            filled += read;
        }

        return filled;
    }
}
