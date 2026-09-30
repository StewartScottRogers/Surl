using System.Buffers.Binary;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Reads binary packets (RFC 4253, section 6) from the client, one after another, opens each
/// with the <see cref="Protection"/> in force, and hands back each packet's payload.
/// </summary>
/// <remarks>
/// A packet whose <c>packet_length</c> plus 4 is over the packet limit, is 0, or is not a
/// multiple of the protection's block size (<c>packet_length</c> alone beside an encrypt-then-MAC
/// MAC or an AEAD cipher) is refused from its length field alone, before the rest of it is read
/// (ADR-0006 sections 1 and 5, ADR-0051 decision 9); so is one that leaves fewer than 4 padding
/// bytes or no payload, once read. Each is <c>DISCONNECT</c> 2; a MAC or tag that does not verify
/// is <c>DISCONNECT</c> 5. It is not safe for concurrent calls.
/// </remarks>
/// <param name="reader">The buffered reader over the connection.</param>
/// <param name="maxPacketBytes">The most bytes a packet may hold, its length field included; 0 means no limit.</param>
internal sealed class SshPacketReader(SshConnectionReader reader, long maxPacketBytes)
{
    /// <summary>
    /// The block size packets are padded to before a cipher is agreed (RFC 4253, section 6).
    /// </summary>
    public const int BlockSize = 8;

    /// <summary>
    /// The fewest padding bytes a packet may carry (RFC 4253, section 6).
    /// </summary>
    public const int MinPaddingBytes = 4;

    private const int LengthFieldBytes = 4;

    /// <summary>
    /// The sequence number of the next packet read (RFC 4253, section 6.4): 0 for the first,
    /// one more for each packet, wrapping after 2^32 - 1, and set back to 0 after
    /// <c>NEWKEYS</c> under strict key exchange (ADR-0051, decision 2.1).
    /// </summary>
    public uint SequenceNumber { get; set; }

    /// <summary>
    /// Whether a packet that would wrap <see cref="SequenceNumber"/> is refused with
    /// <c>DISCONNECT</c> 2, as it is during a strict connection's first key exchange.
    /// </summary>
    public bool RefusesSequenceWrap { get; set; }

    /// <summary>
    /// How the client's packets are protected: <see cref="SshPacketProtection.None"/> until the
    /// first <c>NEWKEYS</c> is read.
    /// </summary>
    public SshPacketProtection Protection { get; private set; } = SshPacketProtection.None;

    /// <summary>
    /// How many bytes have been read, MACs and tags included, since <see cref="Protection"/> was last set.
    /// </summary>
    public long BytesSinceNewKeys { get; private set; }

    /// <summary>
    /// Opens every later packet with <paramref name="protection"/>, as the client's <c>NEWKEYS</c>
    /// says, and counts <see cref="BytesSinceNewKeys"/> from 0.
    /// </summary>
    /// <param name="protection">The client-to-server protection just keyed.</param>
    public void UseProtection(SshPacketProtection protection)
    {
        Protection = protection;
        BytesSinceNewKeys = 0;
    }

    /// <summary>
    /// Reads the next packet.
    /// </summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The payload, message number first.</returns>
    /// <exception cref="SshExchangeEndedException">The client closed the connection, between packets or part way through one.</exception>
    /// <exception cref="SshDisconnectRequiredException">The packet is refused: <c>DISCONNECT</c> 2, or 5 for its MAC.</exception>
    public async ValueTask<byte[]> ReadPayloadAsync(CancellationToken cancellationToken)
    {
        var firstByte = await reader.ReadByteAsync(cancellationToken);
        if (firstByte < 0)
        {
            throw new SshExchangeEndedException(null);
        }

        byte[] head = [(byte)firstByte, .. await ReadOrEndAsync(Protection.HeadLength - 1, cancellationToken)];
        var plainHead = Protection.OpenHead(head);
        var packetLength = BinaryPrimitives.ReadUInt32BigEndian(plainHead);
        RefuseBadLength(packetLength);

        var rest = await ReadOrEndAsync((int)packetLength + LengthFieldBytes - head.Length + Protection.TagLength, cancellationToken);
        var body = Protection.OpenBody(SequenceNumber, plainHead, rest);
        var paddingLength = body[0];
        var payloadLength = body.Length - 1 - paddingLength;
        if (paddingLength < MinPaddingBytes || payloadLength < 1)
        {
            throw SshDisconnectRequiredException.ProtocolError(
                $"An SSH packet of {packetLength} bytes announced {paddingLength} padding bytes: fewer than {MinPaddingBytes}, or no room for a message.");
        }

        CountPacket(head.Length + rest.Length);

        return body[1..(1 + payloadLength)];
    }

    private void CountPacket(int bytesRead)
    {
        if (SequenceNumber == uint.MaxValue && RefusesSequenceWrap)
        {
            throw SshDisconnectRequiredException.ProtocolError(
                "The client's SSH packet sequence number would wrap during a strict key exchange.");
        }

        SequenceNumber = unchecked(SequenceNumber + 1);
        BytesSinceNewKeys += bytesRead;
    }

    private void RefuseBadLength(uint packetLength)
    {
        // With no limit set, a packet and its MAC still have to fit in one array.
        var arrayLimit = Array.MaxLength - Protection.TagLength;
        var limit = maxPacketBytes > 0 ? Math.Min(maxPacketBytes, arrayLimit) : arrayLimit;
        var wholeLength = packetLength + (long)LengthFieldBytes;
        if (wholeLength > limit)
        {
            throw SshDisconnectRequiredException.ProtocolError(
                $"An SSH packet announced {wholeLength} bytes, over the {limit}-byte packet limit.");
        }

        var alignedLength = Protection.AlignsLength ? wholeLength : packetLength;
        if (alignedLength % Protection.BlockSize != 0)
        {
            throw SshDisconnectRequiredException.ProtocolError(
                $"An SSH packet announced {wholeLength} bytes, not a multiple of the {Protection.BlockSize}-byte block size.");
        }

        if (packetLength == 0)
        {
            throw SshDisconnectRequiredException.ProtocolError("An SSH packet announced no bytes after its length field.");
        }
    }

    private async ValueTask<byte[]> ReadOrEndAsync(int count, CancellationToken cancellationToken) =>
        await reader.ReadExactlyAsync(count, cancellationToken)
            ?? throw new SshExchangeEndedException("The client closed the connection part way through an SSH packet.");
}
