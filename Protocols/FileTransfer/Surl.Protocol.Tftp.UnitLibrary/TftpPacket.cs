using System.Buffers.Binary;
using System.Text;

namespace Surl.Protocol.Tftp;

/// <summary>
/// The TFTP packets (RFC 1350 section 5, RFC 2347): their opcodes, and the bytes of each
/// packet the server sends.
/// </summary>
internal static class TftpPacket
{
    /// <summary>The opcode of a read request.</summary>
    public const int ReadRequest = 1;

    /// <summary>The opcode of a write request.</summary>
    public const int WriteRequest = 2;

    /// <summary>The opcode of a DATA packet.</summary>
    public const int Data = 3;

    /// <summary>The opcode of an ACK.</summary>
    public const int Acknowledgement = 4;

    /// <summary>The opcode of an ERROR packet.</summary>
    public const int Error = 5;

    /// <summary>The opcode of an option acknowledgement (RFC 2347).</summary>
    public const int OptionAcknowledgement = 6;

    /// <summary>
    /// Returns the opcode <paramref name="datagram"/> starts with.
    /// </summary>
    /// <param name="datagram">A whole datagram.</param>
    /// <returns>The big-endian opcode, or -1 when the datagram is shorter than two bytes.</returns>
    public static int ReadOpcode(ReadOnlySpan<byte> datagram) =>
        datagram.Length < 2 ? -1 : BinaryPrimitives.ReadUInt16BigEndian(datagram);

    /// <summary>
    /// Returns the block number of a DATA packet or an ACK.
    /// </summary>
    /// <param name="datagram">A whole datagram.</param>
    /// <returns>The big-endian block number, or -1 when the datagram is shorter than four bytes.</returns>
    public static int ReadBlockNumber(ReadOnlySpan<byte> datagram) =>
        datagram.Length < 4 ? -1 : BinaryPrimitives.ReadUInt16BigEndian(datagram[2..]);

    /// <summary>
    /// Returns a DATA packet: opcode 3, <paramref name="blockNumber"/>, then <paramref name="payload"/>.
    /// </summary>
    /// <param name="blockNumber">The block number, already wrapped to 16 bits.</param>
    /// <param name="payload">The block's bytes; fewer than the block size in the last block.</param>
    /// <returns>The packet.</returns>
    public static byte[] ForData(ushort blockNumber, ReadOnlySpan<byte> payload)
    {
        var packet = new byte[4 + payload.Length];
        BinaryPrimitives.WriteUInt16BigEndian(packet, Data);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), blockNumber);
        payload.CopyTo(packet.AsSpan(4));

        return packet;
    }

    /// <summary>
    /// Returns an ACK: opcode 4, then <paramref name="blockNumber"/>.
    /// </summary>
    /// <param name="blockNumber">The block number acknowledged, already wrapped to 16 bits.</param>
    /// <returns>The packet.</returns>
    public static byte[] ForAcknowledgement(ushort blockNumber)
    {
        var packet = new byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(packet, Acknowledgement);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), blockNumber);

        return packet;
    }

    /// <summary>
    /// Returns an ERROR packet: opcode 5, <paramref name="errorCode"/>, then
    /// <paramref name="message"/> as ASCII and a zero byte.
    /// </summary>
    /// <param name="errorCode">The RFC 1350 error code.</param>
    /// <param name="message">The error message, printable ASCII.</param>
    /// <returns>The packet.</returns>
    public static byte[] ForError(TftpErrorCode errorCode, string message)
    {
        var packet = new byte[4 + message.Length + 1];
        BinaryPrimitives.WriteUInt16BigEndian(packet, Error);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), (ushort)errorCode);
        Encoding.ASCII.GetBytes(message, packet.AsSpan(4));

        return packet;
    }

    /// <summary>
    /// Returns an OACK: opcode 6, then each accepted option's name and value, each followed
    /// by a zero byte (RFC 2347).
    /// </summary>
    /// <param name="acceptedOptions">The accepted options, in the order the client asked for them.</param>
    /// <returns>The packet.</returns>
    public static byte[] ForOptionAcknowledgement(IEnumerable<TftpOption> acceptedOptions)
    {
        var text = new StringBuilder();
        foreach (var option in acceptedOptions)
        {
            text.Append(option.Name).Append('\0').Append(option.Value).Append('\0');
        }

        var packet = new byte[2 + text.Length];
        BinaryPrimitives.WriteUInt16BigEndian(packet, OptionAcknowledgement);
        Encoding.ASCII.GetBytes(text.ToString(), packet.AsSpan(2));

        return packet;
    }
}
