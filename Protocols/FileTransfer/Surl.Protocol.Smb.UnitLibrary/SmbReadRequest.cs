namespace Surl.Protocol.Smb;

/// <summary>
/// <c>SMB_COM_READ_ANDX</c> ([MS-CIFS] section 2.2.4.42.1), with or without the high offset word.
/// </summary>
/// <param name="Header">The request's header.</param>
/// <param name="FileId">The FID the NT create response gave.</param>
/// <param name="Offset">Where in the file to read from.</param>
/// <param name="MaxCount">The most bytes to return.</param>
/// <param name="MinCount">The fewest bytes to return.</param>
/// <param name="Timeout">The timeout, for pipes.</param>
/// <param name="Remaining">The bytes remaining to satisfy the read.</param>
internal sealed record SmbReadRequest(
    SmbHeader Header,
    ushort FileId,
    long Offset,
    ushort MaxCount,
    ushort MinCount,
    uint Timeout,
    ushort Remaining) : SmbRequest(Header);
