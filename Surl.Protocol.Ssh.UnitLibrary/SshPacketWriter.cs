using System.Buffers.Binary;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Writes unencrypted binary packets (RFC 4253, section 6): <c>packet_length</c>,
/// <c>padding_length</c>, the payload and random padding.
/// </summary>
/// <param name="connection">The connection to write to.</param>
/// <param name="randomSource">Where the padding bytes come from.</param>
internal sealed class SshPacketWriter(IConnection connection, ISshRandomSource randomSource)
{
    /// <summary>
    /// How many padding bytes a payload gets before a cipher is agreed: the fewest, at least
    /// <see cref="SshPacketReader.MinPaddingBytes"/>, that make the whole packet a multiple of
    /// <see cref="SshPacketReader.BlockSize"/> (4 to 11).
    /// </summary>
    /// <param name="payloadLength">The payload's length in bytes.</param>
    /// <returns>The padding length.</returns>
    public static int PaddingLengthFor(int payloadLength)
    {
        var padding = SshPacketReader.BlockSize - ((sizeof(uint) + 1 + payloadLength) % SshPacketReader.BlockSize);

        return padding < SshPacketReader.MinPaddingBytes ? padding + SshPacketReader.BlockSize : padding;
    }

    /// <summary>
    /// Frames <paramref name="payload"/> as one packet and writes it.
    /// </summary>
    /// <param name="payload">The message, message number first.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes once the packet is handed to the transport.</returns>
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var paddingLength = PaddingLengthFor(payload.Length);
        var packet = new byte[sizeof(uint) + 1 + payload.Length + paddingLength];
        BinaryPrimitives.WriteUInt32BigEndian(packet, (uint)(packet.Length - sizeof(uint)));
        packet[sizeof(uint)] = (byte)paddingLength;
        payload.Span.CopyTo(packet.AsSpan(sizeof(uint) + 1));
        randomSource.Fill(packet.AsSpan(packet.Length - paddingLength));

        await connection.WriteAsync(packet, cancellationToken);
    }
}
