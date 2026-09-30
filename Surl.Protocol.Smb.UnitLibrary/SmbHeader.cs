using System.Buffers.Binary;

namespace Surl.Protocol.Smb;

/// <summary>
/// The 32-byte SMB version 1 header every message starts with ([MS-CIFS] section 2.2.3.1):
/// little-endian fields after the <c>0xFF 'SMB'</c> protocol identifier. The security features
/// and reserved fields are not kept; they are written as zeros, as a server that does not sign.
/// </summary>
/// <param name="Command">The SMB command code.</param>
/// <param name="Status">The 32-bit status; 0 is success.</param>
/// <param name="Flags">The <c>Flags</c> byte.</param>
/// <param name="Flags2">The <c>Flags2</c> word.</param>
/// <param name="ProcessIdHigh">The high 16 bits of the process ID.</param>
/// <param name="TreeId">The TID.</param>
/// <param name="ProcessIdLow">The low 16 bits of the process ID.</param>
/// <param name="UserId">The UID.</param>
/// <param name="MultiplexId">The MID.</param>
internal sealed record SmbHeader(
    byte Command,
    uint Status,
    byte Flags,
    ushort Flags2,
    ushort ProcessIdHigh,
    ushort TreeId,
    ushort ProcessIdLow,
    ushort UserId,
    ushort MultiplexId)
{
    /// <summary>The length of the header.</summary>
    public const int Length = 32;

    /// <summary><c>SMB_FLAGS_REPLY</c>: set in every response ([MS-CIFS] section 2.2.3.1).</summary>
    public const byte ReplyFlag = 0x80;

    private const int CommandOffset = 4;
    private const int StatusOffset = 5;
    private const int FlagsOffset = 9;
    private const int Flags2Offset = 10;
    private const int ProcessIdHighOffset = 12;
    private const int TreeIdOffset = 24;
    private const int ProcessIdLowOffset = 26;
    private const int UserIdOffset = 28;
    private const int MultiplexIdOffset = 30;

    /// <summary>The protocol identifier the header opens with: <c>0xFF 'S' 'M' 'B'</c>.</summary>
    public static ReadOnlySpan<byte> ProtocolIdentifier => [0xFF, (byte)'S', (byte)'M', (byte)'B'];

    /// <summary>
    /// Reads the header at the start of <paramref name="message"/>, which the caller has checked
    /// is at least <see cref="Length"/> bytes and opens with <see cref="ProtocolIdentifier"/>.
    /// </summary>
    /// <param name="message">An SMB message, without its NetBIOS header.</param>
    /// <returns>The header.</returns>
    public static SmbHeader Read(ReadOnlySpan<byte> message) =>
        new(
            message[CommandOffset],
            BinaryPrimitives.ReadUInt32LittleEndian(message[StatusOffset..]),
            message[FlagsOffset],
            BinaryPrimitives.ReadUInt16LittleEndian(message[Flags2Offset..]),
            BinaryPrimitives.ReadUInt16LittleEndian(message[ProcessIdHighOffset..]),
            BinaryPrimitives.ReadUInt16LittleEndian(message[TreeIdOffset..]),
            BinaryPrimitives.ReadUInt16LittleEndian(message[ProcessIdLowOffset..]),
            BinaryPrimitives.ReadUInt16LittleEndian(message[UserIdOffset..]),
            BinaryPrimitives.ReadUInt16LittleEndian(message[MultiplexIdOffset..]));

    /// <summary>
    /// The header of the response to the request this header opened: the same command, TID,
    /// PID, UID and MID, as [MS-CIFS] section 2.2.3.1 requires, <c>Flags</c> with
    /// <see cref="ReplyFlag"/> added, <c>Flags2</c> unchanged, and <paramref name="status"/>.
    /// </summary>
    /// <param name="status">The response's status; 0 is success.</param>
    /// <returns>The response header.</returns>
    public SmbHeader ToResponse(uint status) => this with { Status = status, Flags = (byte)(Flags | ReplyFlag) };

    /// <summary>
    /// Writes the header over the first <see cref="Length"/> bytes of <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">At least <see cref="Length"/> bytes.</param>
    public void Write(Span<byte> destination)
    {
        var header = destination[..Length];
        header.Clear();
        ProtocolIdentifier.CopyTo(header);
        header[CommandOffset] = Command;
        BinaryPrimitives.WriteUInt32LittleEndian(header[StatusOffset..], Status);
        header[FlagsOffset] = Flags;
        BinaryPrimitives.WriteUInt16LittleEndian(header[Flags2Offset..], Flags2);
        BinaryPrimitives.WriteUInt16LittleEndian(header[ProcessIdHighOffset..], ProcessIdHigh);
        BinaryPrimitives.WriteUInt16LittleEndian(header[TreeIdOffset..], TreeId);
        BinaryPrimitives.WriteUInt16LittleEndian(header[ProcessIdLowOffset..], ProcessIdLow);
        BinaryPrimitives.WriteUInt16LittleEndian(header[UserIdOffset..], UserId);
        BinaryPrimitives.WriteUInt16LittleEndian(header[MultiplexIdOffset..], MultiplexId);
    }
}
