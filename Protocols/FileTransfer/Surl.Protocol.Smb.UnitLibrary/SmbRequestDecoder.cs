using System.Buffers.Binary;
using System.Text;

namespace Surl.Protocol.Smb;

/// <summary>
/// Decodes an SMB version 1 message into one of the eight requests upstream curl 8.21.0 sends,
/// checking its word count, byte count, strings and data offset against the layouts of
/// [MS-CIFS] section 2.2.4, and reporting anything malformed as an <see cref="SmbRequestFault"/>
/// rather than throwing.
/// </summary>
/// <remarks>
/// Strings are read as the ASCII curl sends (it never sets <c>SMB_FLAGS2_UNICODE</c>), decoded
/// as UTF-8 so a non-ASCII byte is kept rather than dropped. Bytes after the byte block are
/// ignored as padding. A chained AndX command is reported, never followed.
/// </remarks>
internal static class SmbRequestDecoder
{
    private const int WordCountOffset = SmbHeader.Length;
    private const byte DialectBufferFormat = 0x02;

    private static readonly Dictionary<byte, CommandDecoder> CommandDecoders = new()
    {
        [SmbCommand.Negotiate] = DecodeNegotiate,
        [SmbCommand.SessionSetupAndX] = DecodeSessionSetup,
        [SmbCommand.TreeConnectAndX] = DecodeTreeConnect,
        [SmbCommand.NtCreateAndX] = DecodeNtCreate,
        [SmbCommand.ReadAndX] = DecodeRead,
        [SmbCommand.WriteAndX] = DecodeWrite,
        [SmbCommand.Close] = DecodeClose,
        [SmbCommand.TreeDisconnect] = DecodeTreeDisconnect,
    };

    private delegate SmbRequestDecoding CommandDecoder(in SmbRequestBlocks blocks);

    /// <summary>
    /// Decodes <paramref name="message"/>.
    /// </summary>
    /// <param name="message">One SMB message, without its NetBIOS header.</param>
    /// <returns>The request, or the fault and, when it could be read, the header.</returns>
    public static SmbRequestDecoding Decode(ReadOnlySpan<byte> message)
    {
        if (message.Length < SmbHeader.Length)
        {
            return SmbRequestDecoding.Refused(SmbRequestFault.Truncated, null);
        }

        if (!message.StartsWith(SmbHeader.ProtocolIdentifier))
        {
            return SmbRequestDecoding.Refused(SmbRequestFault.BadSignature, null);
        }

        var header = SmbHeader.Read(message);
        var fault = ReadBlocks(message, out var parameters, out var bytesStart, out var bytes);

        return fault == SmbRequestFault.None
            ? DecodeCommand(new SmbRequestBlocks(header, parameters, bytesStart, bytes, message))
            : SmbRequestDecoding.Refused(fault, header);
    }

    private static SmbRequestFault ReadBlocks(
        ReadOnlySpan<byte> message,
        out ReadOnlySpan<byte> parameters,
        out int bytesStart,
        out ReadOnlySpan<byte> bytes)
    {
        parameters = default;
        bytes = default;
        bytesStart = 0;
        if (message.Length <= WordCountOffset)
        {
            return SmbRequestFault.Truncated;
        }

        var parametersLength = 2 * message[WordCountOffset];
        var byteCountOffset = WordCountOffset + 1 + parametersLength;
        if (message.Length < byteCountOffset + 2)
        {
            return SmbRequestFault.Truncated;
        }

        bytesStart = byteCountOffset + 2;
        var byteCount = BinaryPrimitives.ReadUInt16LittleEndian(message[byteCountOffset..]);
        if (message.Length < bytesStart + byteCount)
        {
            return SmbRequestFault.InconsistentByteCount;
        }

        parameters = message.Slice(WordCountOffset + 1, parametersLength);
        bytes = message.Slice(bytesStart, byteCount);
        return SmbRequestFault.None;
    }

    private static SmbRequestDecoding DecodeCommand(in SmbRequestBlocks blocks) =>
        CommandDecoders.TryGetValue(blocks.Header.Command, out var decodeCommand)
            ? decodeCommand(blocks)
            : blocks.Refuse(SmbRequestFault.UnsupportedCommand);

    private static SmbRequestDecoding DecodeNegotiate(in SmbRequestBlocks blocks)
    {
        if (!blocks.Parameters.IsEmpty)
        {
            return blocks.Refuse(SmbRequestFault.InconsistentWordCount);
        }

        var dialects = new List<string>();
        var offset = 0;
        while (offset < blocks.Bytes.Length)
        {
            if (blocks.Bytes[offset] != DialectBufferFormat)
            {
                return blocks.Refuse(SmbRequestFault.MalformedString);
            }

            offset++;
            if (!TryReadString(blocks.Bytes, ref offset, out var dialect))
            {
                return blocks.Refuse(SmbRequestFault.MalformedString);
            }

            dialects.Add(dialect);
        }

        return SmbRequestDecoding.Decoded(new SmbNegotiateRequest(blocks.Header, dialects));
    }

    private static SmbRequestDecoding DecodeSessionSetup(in SmbRequestBlocks blocks)
    {
        var fault = CheckAndXWordCount(blocks, 13, 13);
        if (fault != SmbRequestFault.None)
        {
            return blocks.Refuse(fault);
        }

        var parameters = blocks.Parameters;
        var lmLength = BinaryPrimitives.ReadUInt16LittleEndian(parameters[14..]);
        var ntLength = BinaryPrimitives.ReadUInt16LittleEndian(parameters[16..]);
        if (lmLength + ntLength > blocks.Bytes.Length)
        {
            return blocks.Refuse(SmbRequestFault.InconsistentByteCount);
        }

        if (!TryReadStrings(blocks.Bytes, lmLength + ntLength, 4, out var strings))
        {
            return blocks.Refuse(SmbRequestFault.MalformedString);
        }

        return SmbRequestDecoding.Decoded(new SmbSessionSetupRequest(
            blocks.Header,
            BinaryPrimitives.ReadUInt16LittleEndian(parameters[4..]),
            BinaryPrimitives.ReadUInt16LittleEndian(parameters[6..]),
            BinaryPrimitives.ReadUInt16LittleEndian(parameters[8..]),
            BinaryPrimitives.ReadUInt32LittleEndian(parameters[10..]),
            BinaryPrimitives.ReadUInt32LittleEndian(parameters[22..]),
            blocks.Bytes[..lmLength].ToArray(),
            blocks.Bytes.Slice(lmLength, ntLength).ToArray(),
            strings[0],
            strings[1],
            strings[2],
            strings[3]));
    }

    private static SmbRequestDecoding DecodeTreeConnect(in SmbRequestBlocks blocks)
    {
        var fault = CheckAndXWordCount(blocks, 4, 4);
        if (fault != SmbRequestFault.None)
        {
            return blocks.Refuse(fault);
        }

        var passwordLength = BinaryPrimitives.ReadUInt16LittleEndian(blocks.Parameters[6..]);
        if (passwordLength > blocks.Bytes.Length)
        {
            return blocks.Refuse(SmbRequestFault.InconsistentByteCount);
        }

        if (!TryReadStrings(blocks.Bytes, passwordLength, 2, out var strings))
        {
            return blocks.Refuse(SmbRequestFault.MalformedString);
        }

        return SmbRequestDecoding.Decoded(new SmbTreeConnectRequest(
            blocks.Header,
            BinaryPrimitives.ReadUInt16LittleEndian(blocks.Parameters[4..]),
            blocks.Bytes[..passwordLength].ToArray(),
            strings[0],
            strings[1]));
    }

    private static SmbRequestDecoding DecodeNtCreate(in SmbRequestBlocks blocks)
    {
        var fault = CheckAndXWordCount(blocks, 24, 24);
        if (fault != SmbRequestFault.None)
        {
            return blocks.Refuse(fault);
        }

        var parameters = blocks.Parameters;
        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(parameters[5..]);
        if (nameLength > blocks.Bytes.Length)
        {
            return blocks.Refuse(SmbRequestFault.InconsistentByteCount);
        }

        return SmbRequestDecoding.Decoded(new SmbNtCreateRequest(
            blocks.Header,
            BinaryPrimitives.ReadUInt32LittleEndian(parameters[7..]),
            BinaryPrimitives.ReadUInt32LittleEndian(parameters[11..]),
            BinaryPrimitives.ReadUInt32LittleEndian(parameters[15..]),
            BinaryPrimitives.ReadInt64LittleEndian(parameters[19..]),
            BinaryPrimitives.ReadUInt32LittleEndian(parameters[27..]),
            BinaryPrimitives.ReadUInt32LittleEndian(parameters[31..]),
            BinaryPrimitives.ReadUInt32LittleEndian(parameters[35..]),
            BinaryPrimitives.ReadUInt32LittleEndian(parameters[39..]),
            BinaryPrimitives.ReadUInt32LittleEndian(parameters[43..]),
            parameters[47],
            Encoding.UTF8.GetString(blocks.Bytes[..nameLength].TrimEnd((byte)0))));
    }

    private static SmbRequestDecoding DecodeRead(in SmbRequestBlocks blocks)
    {
        var fault = CheckAndXWordCount(blocks, 10, 12);
        if (fault != SmbRequestFault.None)
        {
            return blocks.Refuse(fault);
        }

        var parameters = blocks.Parameters;
        return SmbRequestDecoding.Decoded(new SmbReadRequest(
            blocks.Header,
            BinaryPrimitives.ReadUInt16LittleEndian(parameters[4..]),
            ReadOffset(parameters, 6, 20),
            BinaryPrimitives.ReadUInt16LittleEndian(parameters[10..]),
            BinaryPrimitives.ReadUInt16LittleEndian(parameters[12..]),
            BinaryPrimitives.ReadUInt32LittleEndian(parameters[14..]),
            BinaryPrimitives.ReadUInt16LittleEndian(parameters[18..])));
    }

    private static SmbRequestDecoding DecodeWrite(in SmbRequestBlocks blocks)
    {
        var fault = CheckAndXWordCount(blocks, 12, 14);
        if (fault != SmbRequestFault.None)
        {
            return blocks.Refuse(fault);
        }

        var parameters = blocks.Parameters;
        var dataLength = BinaryPrimitives.ReadUInt16LittleEndian(parameters[20..]);
        var dataOffset = BinaryPrimitives.ReadUInt16LittleEndian(parameters[22..]);
        if (dataOffset < blocks.BytesStart || dataOffset + dataLength > blocks.Message.Length)
        {
            return blocks.Refuse(SmbRequestFault.OffsetOutsideMessage);
        }

        return SmbRequestDecoding.Decoded(new SmbWriteRequest(
            blocks.Header,
            BinaryPrimitives.ReadUInt16LittleEndian(parameters[4..]),
            ReadOffset(parameters, 6, 24),
            BinaryPrimitives.ReadUInt32LittleEndian(parameters[10..]),
            BinaryPrimitives.ReadUInt16LittleEndian(parameters[14..]),
            BinaryPrimitives.ReadUInt16LittleEndian(parameters[16..]),
            blocks.Message.Slice(dataOffset, dataLength).ToArray()));
    }

    private static SmbRequestDecoding DecodeClose(in SmbRequestBlocks blocks)
    {
        if (blocks.Parameters.Length != 6)
        {
            return blocks.Refuse(SmbRequestFault.InconsistentWordCount);
        }

        return SmbRequestDecoding.Decoded(new SmbCloseRequest(
            blocks.Header,
            BinaryPrimitives.ReadUInt16LittleEndian(blocks.Parameters),
            BinaryPrimitives.ReadUInt32LittleEndian(blocks.Parameters[2..])));
    }

    private static SmbRequestDecoding DecodeTreeDisconnect(in SmbRequestBlocks blocks) =>
        blocks.Parameters.IsEmpty
            ? SmbRequestDecoding.Decoded(new SmbTreeDisconnectRequest(blocks.Header))
            : blocks.Refuse(SmbRequestFault.InconsistentWordCount);

    // An AndX request has one of two word counts (the same twice when it has one) and ends its
    // chain with SMB_COM_NO_ANDX_COMMAND in its first parameter byte.
    private static SmbRequestFault CheckAndXWordCount(in SmbRequestBlocks blocks, int wordCount, int longWordCount)
    {
        var words = blocks.Parameters.Length / 2;
        if (words != wordCount && words != longWordCount)
        {
            return SmbRequestFault.InconsistentWordCount;
        }

        return blocks.Parameters[0] == SmbCommand.NoAndXCommand ? SmbRequestFault.None : SmbRequestFault.ChainedAndXCommand;
    }

    // The 32-bit offset, with the high 32 bits when the parameters are long enough to hold them.
    private static long ReadOffset(ReadOnlySpan<byte> parameters, int lowOffset, int highOffset)
    {
        long offset = BinaryPrimitives.ReadUInt32LittleEndian(parameters[lowOffset..]);
        return parameters.Length > highOffset
            ? offset | ((long)BinaryPrimitives.ReadUInt32LittleEndian(parameters[highOffset..]) << 32)
            : offset;
    }

    private static bool TryReadStrings(ReadOnlySpan<byte> bytes, int offset, int count, out string[] strings)
    {
        strings = new string[count];
        for (var index = 0; index < count; index++)
        {
            if (!TryReadString(bytes, ref offset, out strings[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadString(ReadOnlySpan<byte> bytes, ref int offset, out string text)
    {
        var length = bytes[offset..].IndexOf((byte)0);
        if (length < 0)
        {
            text = string.Empty;
            return false;
        }

        text = Encoding.UTF8.GetString(bytes.Slice(offset, length));
        offset += length + 1;
        return true;
    }

    private readonly ref struct SmbRequestBlocks(
        SmbHeader header,
        ReadOnlySpan<byte> parameters,
        int bytesStart,
        ReadOnlySpan<byte> bytes,
        ReadOnlySpan<byte> message)
    {
        public SmbHeader Header { get; } = header;

        public ReadOnlySpan<byte> Parameters { get; } = parameters;

        public int BytesStart { get; } = bytesStart;

        public ReadOnlySpan<byte> Bytes { get; } = bytes;

        public ReadOnlySpan<byte> Message { get; } = message;

        public SmbRequestDecoding Refuse(SmbRequestFault fault) => SmbRequestDecoding.Refused(fault, Header);
    }
}
