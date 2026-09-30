using System.Buffers.Binary;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Writes binary packets (RFC 4253, section 6) - <c>packet_length</c>, <c>padding_length</c>,
/// the payload and random padding - sealed with the <see cref="Protection"/> in force.
/// </summary>
/// <param name="connection">The connection to write to.</param>
/// <param name="randomSource">Where the padding bytes come from.</param>
internal sealed class SshPacketWriter(IConnection connection, ISshRandomSource randomSource) : IDisposable
{
    private SshZlibCompressor? compressor;

    /// <summary>
    /// How many padding bytes a payload gets before a cipher is agreed: the fewest, at least
    /// <see cref="SshPacketReader.MinPaddingBytes"/>, that make the whole packet a multiple of
    /// <see cref="SshPacketReader.BlockSize"/> (4 to 11).
    /// </summary>
    /// <param name="payloadLength">The payload's length in bytes.</param>
    /// <returns>The padding length.</returns>
    public static int PaddingLengthFor(int payloadLength) => PaddingLengthFor(payloadLength, SshPacketProtection.None);

    /// <summary>
    /// How many padding bytes a payload gets under <paramref name="protection"/>: the fewest, at
    /// least <see cref="SshPacketReader.MinPaddingBytes"/>, that make the packet - without its
    /// <c>packet_length</c> when the protection does not align it - a multiple of the block size.
    /// </summary>
    /// <param name="payloadLength">The payload's length in bytes.</param>
    /// <param name="protection">The protection the packet is sealed with.</param>
    /// <returns>The padding length.</returns>
    public static int PaddingLengthFor(int payloadLength, SshPacketProtection protection)
    {
        var alignedBytes = (protection.AlignsLength ? sizeof(uint) : 0) + 1 + payloadLength;
        var padding = protection.BlockSize - (alignedBytes % protection.BlockSize);

        return padding < SshPacketReader.MinPaddingBytes ? padding + protection.BlockSize : padding;
    }

    /// <summary>
    /// The sequence number of the next packet written (RFC 4253, section 6.4): 0 for the
    /// first, one more for each packet, wrapping after 2^32 - 1, and set back to 0 after
    /// <c>NEWKEYS</c> under strict key exchange (ADR-0051, decision 2.1). The server writes
    /// four packets in its first key exchange, so its own number cannot wrap there.
    /// </summary>
    public uint SequenceNumber { get; set; }

    /// <summary>
    /// How the server's packets are protected: <see cref="SshPacketProtection.None"/> until the
    /// first <c>NEWKEYS</c> is written.
    /// </summary>
    public SshPacketProtection Protection { get; private set; } = SshPacketProtection.None;

    /// <summary>
    /// How many bytes have been written, MACs and tags included, since <see cref="Protection"/> was last set.
    /// </summary>
    public long BytesSinceNewKeys { get; private set; }

    /// <summary>
    /// Seals every later packet with <paramref name="protection"/>, as the server's <c>NEWKEYS</c>
    /// says, and counts <see cref="BytesSinceNewKeys"/> from 0.
    /// </summary>
    /// <param name="protection">The server-to-client protection just keyed.</param>
    public void UseProtection(SshPacketProtection protection)
    {
        Protection = protection;
        BytesSinceNewKeys = 0;
    }

    /// <summary>
    /// Compresses every later payload with a new zlib stream when <paramref name="compresses"/>,
    /// else none; either way the stream before it ends (RFC 4253, section 6.2).
    /// </summary>
    /// <param name="compresses">Whether later payloads are compressed.</param>
    public void UseCompression(bool compresses)
    {
        compressor?.Dispose();
        compressor = compresses ? new SshZlibCompressor() : null;
    }

    /// <inheritdoc/>
    public void Dispose() => compressor?.Dispose();

    /// <summary>
    /// Frames <paramref name="message"/> as one packet, compressed if compression is in use,
    /// seals it and writes it.
    /// </summary>
    /// <param name="message">The message, message number first.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes once the packet is handed to the transport.</returns>
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> payload = compressor is null ? message : compressor.Compress(message.Span);
        var paddingLength = PaddingLengthFor(payload.Length, Protection);
        var packet = new byte[sizeof(uint) + 1 + payload.Length + paddingLength];
        BinaryPrimitives.WriteUInt32BigEndian(packet, (uint)(packet.Length - sizeof(uint)));
        packet[sizeof(uint)] = (byte)paddingLength;
        payload.Span.CopyTo(packet.AsSpan(sizeof(uint) + 1));
        randomSource.Fill(packet.AsSpan(packet.Length - paddingLength));

        var sealedPacket = Protection.Seal(SequenceNumber, packet);
        SequenceNumber = unchecked(SequenceNumber + 1);
        BytesSinceNewKeys += sealedPacket.Length;
        await connection.WriteAsync(sealedPacket, cancellationToken);
    }
}
