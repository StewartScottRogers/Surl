using System.Buffers.Binary;
using System.Text;

namespace Surl.Protocol.Smb;

/// <summary>
/// Encodes the SMB version 1 responses to the eight requests upstream curl 8.21.0 sends, each
/// framed as a NetBIOS session message (RFC 1002 section 4.3) ready to write to the connection.
/// </summary>
/// <remarks>
/// Every response's header is the request's, turned round by <see cref="SmbHeader.ToResponse"/>:
/// TID, PID, UID and MID echoed, except that a session setup response assigns the UID and a tree
/// connect response the TID. AndX responses end their chain with <c>SMB_COM_NO_ANDX_COMMAND</c>
/// and an AndX offset of 0, which [MS-CIFS] section 2.2.3.4 has the receiver ignore. Strings are
/// written NUL-terminated in the request's non-Unicode form, as UTF-8.
/// </remarks>
internal static class SmbResponseEncoder
{
    // Header, word count, 12 parameter words, byte count and one pad byte: where a read
    // response's data starts, counted from the SMB header.
    private const ushort ReadDataOffset = SmbHeader.Length + 1 + 24 + 2 + 1;

    /// <summary>
    /// Encodes the negotiate response: the NT LM 0.12 form with the challenge and domain name.
    /// </summary>
    /// <param name="request">The negotiate request's header.</param>
    /// <param name="response">The server's choices.</param>
    /// <returns>The framed response.</returns>
    public static byte[] EncodeNegotiate(SmbHeader request, SmbNegotiateResponse response)
    {
        var parameters = new byte[34];
        BinaryPrimitives.WriteUInt16LittleEndian(parameters, response.DialectIndex);
        parameters[2] = response.SecurityMode;
        BinaryPrimitives.WriteUInt16LittleEndian(parameters.AsSpan(3), response.MaxMpxCount);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters.AsSpan(5), response.MaxNumberVirtualCircuits);
        BinaryPrimitives.WriteUInt32LittleEndian(parameters.AsSpan(7), response.MaxBufferSize);
        BinaryPrimitives.WriteUInt32LittleEndian(parameters.AsSpan(11), response.MaxRawSize);
        BinaryPrimitives.WriteUInt32LittleEndian(parameters.AsSpan(15), response.SessionKey);
        BinaryPrimitives.WriteUInt32LittleEndian(parameters.AsSpan(19), response.Capabilities);
        BinaryPrimitives.WriteInt64LittleEndian(parameters.AsSpan(23), response.SystemTime);
        BinaryPrimitives.WriteInt16LittleEndian(parameters.AsSpan(31), response.ServerTimeZone);
        parameters[33] = checked((byte)response.Challenge.Length);

        return Frame(request.ToResponse(0), parameters, [.. response.Challenge, .. Terminated(response.DomainName)]);
    }

    /// <summary>
    /// Encodes the session setup response, assigning the session's UID.
    /// </summary>
    /// <param name="request">The session setup request's header.</param>
    /// <param name="userId">The UID the client uses from now on.</param>
    /// <param name="action">The action bits; 1 means the client was logged on as guest.</param>
    /// <param name="nativeOperatingSystem">The server's operating system.</param>
    /// <param name="nativeLanManager">The server's LAN manager.</param>
    /// <param name="primaryDomain">The server's domain.</param>
    /// <returns>The framed response.</returns>
    public static byte[] EncodeSessionSetup(
        SmbHeader request,
        ushort userId,
        ushort action,
        string nativeOperatingSystem,
        string nativeLanManager,
        string primaryDomain)
    {
        var parameters = AndXParameters(6);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters.AsSpan(4), action);

        return Frame(
            request.ToResponse(0) with { UserId = userId },
            parameters,
            [.. Terminated(nativeOperatingSystem), .. Terminated(nativeLanManager), .. Terminated(primaryDomain)]);
    }

    /// <summary>
    /// Encodes the tree connect response, assigning the tree's TID.
    /// </summary>
    /// <param name="request">The tree connect request's header.</param>
    /// <param name="treeId">The TID the client uses from now on.</param>
    /// <param name="optionalSupport">The optional support bits.</param>
    /// <param name="service">The service connected to, such as <c>A:</c> for a disk share.</param>
    /// <param name="nativeFileSystem">The share's file system, such as <c>NTFS</c>.</param>
    /// <returns>The framed response.</returns>
    public static byte[] EncodeTreeConnect(SmbHeader request, ushort treeId, ushort optionalSupport, string service, string nativeFileSystem)
    {
        var parameters = AndXParameters(6);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters.AsSpan(4), optionalSupport);

        return Frame(
            request.ToResponse(0) with { TreeId = treeId },
            parameters,
            [.. Terminated(service), .. Terminated(nativeFileSystem)]);
    }

    /// <summary>
    /// Encodes the NT create response.
    /// </summary>
    /// <param name="request">The NT create request's header.</param>
    /// <param name="response">The opened file's values.</param>
    /// <returns>The framed response.</returns>
    public static byte[] EncodeNtCreate(SmbHeader request, SmbNtCreateResponse response)
    {
        var parameters = AndXParameters(68);
        var span = parameters.AsSpan();
        span[4] = response.OplockLevel;
        BinaryPrimitives.WriteUInt16LittleEndian(span[5..], response.FileId);
        BinaryPrimitives.WriteUInt32LittleEndian(span[7..], response.CreateAction);
        BinaryPrimitives.WriteInt64LittleEndian(span[11..], response.CreationTime);
        BinaryPrimitives.WriteInt64LittleEndian(span[19..], response.LastAccessTime);
        BinaryPrimitives.WriteInt64LittleEndian(span[27..], response.LastWriteTime);
        BinaryPrimitives.WriteInt64LittleEndian(span[35..], response.LastChangeTime);
        BinaryPrimitives.WriteUInt32LittleEndian(span[43..], response.ExtendedFileAttributes);
        BinaryPrimitives.WriteInt64LittleEndian(span[47..], response.AllocationSize);
        BinaryPrimitives.WriteInt64LittleEndian(span[55..], response.EndOfFile);
        BinaryPrimitives.WriteUInt16LittleEndian(span[63..], response.ResourceType);
        BinaryPrimitives.WriteUInt16LittleEndian(span[65..], response.NamedPipeStatus);
        span[67] = response.IsDirectory ? (byte)1 : (byte)0;

        return Frame(request.ToResponse(0), parameters, []);
    }

    /// <summary>
    /// Encodes the read response: the data after one pad byte, with its offset and length.
    /// </summary>
    /// <param name="request">The read request's header.</param>
    /// <param name="available">The <c>Available</c> field; [MS-CIFS] gives it meaning only for pipes.</param>
    /// <param name="data">The bytes read; fewer than 65535, so the byte count, which counts the pad byte, fits.</param>
    /// <returns>The framed response.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="data"/> is 65535 bytes or more.</exception>
    public static byte[] EncodeRead(SmbHeader request, ushort available, ReadOnlySpan<byte> data)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(data.Length, (int)ushort.MaxValue, nameof(data));

        var parameters = AndXParameters(24);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters.AsSpan(4), available);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters.AsSpan(10), (ushort)data.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters.AsSpan(12), ReadDataOffset);

        return Frame(request.ToResponse(0), parameters, [0, .. data]);
    }

    /// <summary>
    /// Encodes the write response.
    /// </summary>
    /// <param name="request">The write request's header.</param>
    /// <param name="count">How many bytes were written.</param>
    /// <param name="available">The <c>Available</c> field; [MS-CIFS] gives it meaning only for pipes.</param>
    /// <returns>The framed response.</returns>
    public static byte[] EncodeWrite(SmbHeader request, ushort count, ushort available)
    {
        var parameters = AndXParameters(12);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters.AsSpan(4), count);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters.AsSpan(6), available);

        return Frame(request.ToResponse(0), parameters, []);
    }

    /// <summary>
    /// Encodes the close response: success, no parameters, no data.
    /// </summary>
    /// <param name="request">The close request's header.</param>
    /// <returns>The framed response.</returns>
    public static byte[] EncodeClose(SmbHeader request) => EncodeError(request, 0);

    /// <summary>
    /// Encodes the tree disconnect response: success, no parameters, no data.
    /// </summary>
    /// <param name="request">The tree disconnect request's header.</param>
    /// <returns>The framed response.</returns>
    public static byte[] EncodeTreeDisconnect(SmbHeader request) => EncodeError(request, 0);

    /// <summary>
    /// Encodes an error response ([MS-CIFS] section 2.2.4 and 3.3.4.1.2): the status in the
    /// header, word count 0, byte count 0.
    /// </summary>
    /// <param name="request">The failed request's header.</param>
    /// <param name="status">The status; an NT status, or a DOS error class and code.</param>
    /// <returns>The framed response.</returns>
    public static byte[] EncodeError(SmbHeader request, uint status) => Frame(request.ToResponse(status), [], []);

    /// <summary>
    /// Frames an SMB message: the NetBIOS session message header, <paramref name="header"/>, the
    /// word count, <paramref name="parameters"/>, the byte count and <paramref name="bytes"/>.
    /// </summary>
    /// <param name="header">The message's header.</param>
    /// <param name="parameters">The parameter words; an even number of bytes.</param>
    /// <param name="bytes">The data block.</param>
    /// <returns>The bytes to write.</returns>
    public static byte[] Frame(SmbHeader header, ReadOnlySpan<byte> parameters, ReadOnlySpan<byte> bytes)
    {
        var bodyLength = SmbHeader.Length + 1 + parameters.Length + 2 + bytes.Length;
        var message = new byte[SmbFrameReader.FrameHeaderLength + bodyLength];
        var span = message.AsSpan();
        span[0] = SmbFrameReader.SessionMessageType;
        span[1] = (byte)((bodyLength >> 16) & 0x01);
        BinaryPrimitives.WriteUInt16BigEndian(span[2..], (ushort)bodyLength);
        span = span[SmbFrameReader.FrameHeaderLength..];
        header.Write(span);
        span[SmbHeader.Length] = (byte)(parameters.Length / 2);
        parameters.CopyTo(span[(SmbHeader.Length + 1)..]);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(SmbHeader.Length + 1 + parameters.Length)..], (ushort)bytes.Length);
        bytes.CopyTo(span[(SmbHeader.Length + 3 + parameters.Length)..]);
        return message;
    }

    // The parameter words of an AndX response, with no further command.
    private static byte[] AndXParameters(int length)
    {
        var parameters = new byte[length];
        parameters[0] = SmbCommand.NoAndXCommand;
        return parameters;
    }

    private static byte[] Terminated(string text) => [.. Encoding.UTF8.GetBytes(text), 0];
}
