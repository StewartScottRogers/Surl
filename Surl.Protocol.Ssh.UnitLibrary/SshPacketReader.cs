using System.Buffers.Binary;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Reads unencrypted binary packets (RFC 4253, section 6) from the client, one after another,
/// and hands back each packet's payload.
/// </summary>
/// <remarks>
/// Before a cipher is agreed the block size is 8. A packet whose <c>packet_length</c> plus 4
/// is over the packet limit, or is not a multiple of the block size, is refused from its
/// length field alone, before any of its body is read (ADR-0006 sections 1 and 5, ADR-0051
/// decision 9); so is one that leaves fewer than 4 padding bytes or no payload, once read.
/// Each is <c>DISCONNECT</c> 2. It is not safe for concurrent calls.
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
    /// Reads the next packet.
    /// </summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The payload, message number first.</returns>
    /// <exception cref="SshExchangeEndedException">The client closed the connection, between packets or part way through one.</exception>
    /// <exception cref="SshDisconnectRequiredException">The packet is refused: <c>DISCONNECT</c> 2.</exception>
    public async ValueTask<byte[]> ReadPayloadAsync(CancellationToken cancellationToken)
    {
        var firstByte = await reader.ReadByteAsync(cancellationToken);
        if (firstByte < 0)
        {
            throw new SshExchangeEndedException(null);
        }

        var lengthField = await ReadOrEndAsync(LengthFieldBytes - 1, cancellationToken);
        var packetLength = ((uint)firstByte << 24) | (uint)(lengthField[0] << 16) | BinaryPrimitives.ReadUInt16BigEndian(lengthField.AsSpan(1));
        RefuseBadLength(packetLength);

        var body = await ReadOrEndAsync((int)packetLength, cancellationToken);
        var paddingLength = body[0];
        var payloadLength = body.Length - 1 - paddingLength;
        if (paddingLength < MinPaddingBytes || payloadLength < 1)
        {
            throw SshDisconnectRequiredException.ProtocolError(
                $"An SSH packet of {packetLength} bytes announced {paddingLength} padding bytes: fewer than {MinPaddingBytes}, or no room for a message.");
        }

        return body[1..(1 + payloadLength)];
    }

    private void RefuseBadLength(uint packetLength)
    {
        // With no limit set, a packet still has to fit in one array.
        var limit = maxPacketBytes > 0 ? Math.Min(maxPacketBytes, Array.MaxLength) : Array.MaxLength;
        var wholeLength = packetLength + (long)LengthFieldBytes;
        if (wholeLength > limit)
        {
            throw SshDisconnectRequiredException.ProtocolError(
                $"An SSH packet announced {wholeLength} bytes, over the {limit}-byte packet limit.");
        }

        if (wholeLength % BlockSize != 0)
        {
            throw SshDisconnectRequiredException.ProtocolError(
                $"An SSH packet announced {wholeLength} bytes, not a multiple of the {BlockSize}-byte block size.");
        }
    }

    private async ValueTask<byte[]> ReadOrEndAsync(int count, CancellationToken cancellationToken) =>
        await reader.ReadExactlyAsync(count, cancellationToken)
            ?? throw new SshExchangeEndedException("The client closed the connection part way through an SSH packet.");
}
