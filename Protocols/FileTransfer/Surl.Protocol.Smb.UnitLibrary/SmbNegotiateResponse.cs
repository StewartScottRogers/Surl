namespace Surl.Protocol.Smb;

/// <summary>
/// The server's choices in an NT LM 0.12 <c>SMB_COM_NEGOTIATE</c> response without extended
/// security ([MS-CIFS] section 2.2.4.52.2).
/// </summary>
/// <param name="DialectIndex">The index of the chosen dialect in the request's list.</param>
/// <param name="SecurityMode">The security mode bits (user-level, challenge/response).</param>
/// <param name="MaxMpxCount">How many requests the client may have outstanding.</param>
/// <param name="MaxNumberVirtualCircuits">How many virtual circuits the server allows.</param>
/// <param name="MaxBufferSize">The largest message the server accepts.</param>
/// <param name="MaxRawSize">The largest raw buffer the server accepts.</param>
/// <param name="SessionKey">The session key the client echoes in its session setup.</param>
/// <param name="Capabilities">The server's capabilities.</param>
/// <param name="SystemTime">The server's time, as a Windows <c>FILETIME</c>.</param>
/// <param name="ServerTimeZone">The server's time zone, in minutes from UTC.</param>
/// <param name="Challenge">The challenge the client's LM and NT responses answer; at most 255 bytes.</param>
/// <param name="DomainName">The server's domain or workgroup.</param>
internal sealed record SmbNegotiateResponse(
    ushort DialectIndex,
    byte SecurityMode,
    ushort MaxMpxCount,
    ushort MaxNumberVirtualCircuits,
    uint MaxBufferSize,
    uint MaxRawSize,
    uint SessionKey,
    uint Capabilities,
    long SystemTime,
    short ServerTimeZone,
    byte[] Challenge,
    string DomainName);
