namespace Surl.Protocol.Smb;

/// <summary>
/// The DOS-class statuses the SMB server answers with ([MS-CIFS] section 2.2.2.4), as the
/// 32-bit header field holds them: the error class in the low byte and the error code in the
/// high 16 bits. Upstream curl never sets <c>SMB_FLAGS2_NT_STATUS</c>, so it reads these
/// (ADR-0073, decision 4).
/// </summary>
internal static class SmbStatus
{
    /// <summary>Success.</summary>
    public const uint Success = 0;

    /// <summary><c>ERRDOS/ERRbadfile</c>: the file is absent, hidden or refused; curl exits 78.</summary>
    public const uint BadFile = 0x00020001;

    /// <summary><c>ERRDOS/ERRbadfid</c>: the FID is not open.</summary>
    public const uint BadFileId = 0x00060001;

    /// <summary><c>ERRDOS/ERRnofids</c>: the session already has as many files open as it may.</summary>
    public const uint NoFileIds = 0x00040001;

    /// <summary><c>ERRDOS/ERRnoaccess</c>: the open asked for access the server does not grant; curl exits 9.</summary>
    public const uint NoAccess = 0x00050001;

    /// <summary><c>ERRDOS/ERRbadaccess</c>: a write on a file opened for reading, or a read on one opened for writing.</summary>
    public const uint BadAccess = 0x000C0001;

    /// <summary><c>ERRDOS/ERRbadpath</c>: an upload into a directory that does not exist; curl exits 78.</summary>
    public const uint BadPath = 0x00030001;

    /// <summary><c>ERRHRD/ERRgeneral</c>: the content store failed to read or write the file.</summary>
    public const uint GeneralFailure = 0x001F0003;

    /// <summary><c>ERRHRD/ERRdiskfull</c>: a write past <c>--max-filesize</c>; curl exits 25.</summary>
    public const uint DiskFull = 0x00270003;

    /// <summary><c>ERRSRV/ERRerror</c>: a message that does not decode, past <c>--max-message</c>, or out of turn.</summary>
    public const uint ServerError = 0x00010002;

    /// <summary><c>ERRSRV/ERRbadpw</c>: the session setup was refused; curl exits 67.</summary>
    public const uint BadPassword = 0x00020002;

    /// <summary><c>ERRSRV/ERRinvtid</c>: the TID is not connected.</summary>
    public const uint InvalidTreeId = 0x00050002;

    /// <summary><c>ERRSRV/ERRinvnetname</c>: no such share; curl exits 78.</summary>
    public const uint InvalidNetworkName = 0x00060002;

    /// <summary><c>ERRSRV/ERRsmbcmd</c>: a command the server does not serve.</summary>
    public const uint UnsupportedCommand = 0x00400002;

    /// <summary><c>ERRSRV/ERRbaduid</c>: a request before a login, or with another UID.</summary>
    public const uint BadUserId = 0x005B0002;
}
