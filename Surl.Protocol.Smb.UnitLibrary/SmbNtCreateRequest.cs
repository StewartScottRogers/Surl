namespace Surl.Protocol.Smb;

/// <summary>
/// <c>SMB_COM_NT_CREATE_ANDX</c> ([MS-CIFS] section 2.2.4.64.1): opens or creates a file.
/// </summary>
/// <param name="Header">The request's header.</param>
/// <param name="Flags">The create flags.</param>
/// <param name="RootDirectoryFileId">The directory the name is relative to; 0 for the share's root.</param>
/// <param name="DesiredAccess">The access asked for.</param>
/// <param name="AllocationSize">The initial allocation size.</param>
/// <param name="ExtendedFileAttributes">The attributes of a created file.</param>
/// <param name="ShareAccess">The sharing the client allows others.</param>
/// <param name="CreateDisposition">What to do when the file does or does not exist.</param>
/// <param name="CreateOptions">The create options.</param>
/// <param name="ImpersonationLevel">The impersonation level.</param>
/// <param name="SecurityFlags">The security flags.</param>
/// <param name="FileName">The file's path within the share.</param>
internal sealed record SmbNtCreateRequest(
    SmbHeader Header,
    uint Flags,
    uint RootDirectoryFileId,
    uint DesiredAccess,
    long AllocationSize,
    uint ExtendedFileAttributes,
    uint ShareAccess,
    uint CreateDisposition,
    uint CreateOptions,
    uint ImpersonationLevel,
    byte SecurityFlags,
    string FileName) : SmbRequest(Header);
