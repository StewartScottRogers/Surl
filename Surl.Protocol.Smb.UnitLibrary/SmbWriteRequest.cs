namespace Surl.Protocol.Smb;

/// <summary>
/// <c>SMB_COM_WRITE_ANDX</c> ([MS-CIFS] section 2.2.4.43.1), with or without the high offset word.
/// </summary>
/// <param name="Header">The request's header.</param>
/// <param name="FileId">The FID the NT create response gave.</param>
/// <param name="Offset">Where in the file to write.</param>
/// <param name="Timeout">The timeout, for pipes.</param>
/// <param name="WriteMode">The write mode.</param>
/// <param name="Remaining">The bytes remaining to be written.</param>
/// <param name="Data">The bytes to write, taken from where the data offset points.</param>
internal sealed record SmbWriteRequest(
    SmbHeader Header,
    ushort FileId,
    long Offset,
    uint Timeout,
    ushort WriteMode,
    ushort Remaining,
    byte[] Data) : SmbRequest(Header);
