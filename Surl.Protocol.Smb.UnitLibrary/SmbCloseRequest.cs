namespace Surl.Protocol.Smb;

/// <summary>
/// <c>SMB_COM_CLOSE</c> ([MS-CIFS] section 2.2.4.5.1).
/// </summary>
/// <param name="Header">The request's header.</param>
/// <param name="FileId">The FID to close.</param>
/// <param name="LastTimeModified">The modification time to set, in seconds since 1970; 0 or 0xFFFFFFFF leaves it.</param>
internal sealed record SmbCloseRequest(SmbHeader Header, ushort FileId, uint LastTimeModified) : SmbRequest(Header);
