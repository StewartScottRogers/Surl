namespace Surl.Protocol.Smb;

/// <summary>
/// The server's values in an <c>SMB_COM_NT_CREATE_ANDX</c> response ([MS-CIFS] section 2.2.4.64.2).
/// </summary>
/// <param name="OplockLevel">The oplock granted; 0 for none.</param>
/// <param name="FileId">The FID the client reads, writes and closes with.</param>
/// <param name="CreateAction">What the server did: superseded, opened, created or overwritten.</param>
/// <param name="CreationTime">When the file was created, as a Windows <c>FILETIME</c>.</param>
/// <param name="LastAccessTime">When the file was last read, as a Windows <c>FILETIME</c>.</param>
/// <param name="LastWriteTime">When the file was last written, as a Windows <c>FILETIME</c>.</param>
/// <param name="LastChangeTime">When the file last changed, as a Windows <c>FILETIME</c>; curl takes the file's time from it.</param>
/// <param name="ExtendedFileAttributes">The file's attributes.</param>
/// <param name="AllocationSize">The bytes allocated to the file.</param>
/// <param name="EndOfFile">The file's size; curl takes the download's size from it.</param>
/// <param name="ResourceType">The resource type; 0 for a disk file.</param>
/// <param name="NamedPipeStatus">The named pipe state; 0 for a disk file.</param>
/// <param name="IsDirectory">Whether the path names a directory.</param>
internal sealed record SmbNtCreateResponse(
    byte OplockLevel,
    ushort FileId,
    uint CreateAction,
    long CreationTime,
    long LastAccessTime,
    long LastWriteTime,
    long LastChangeTime,
    uint ExtendedFileAttributes,
    long AllocationSize,
    long EndOfFile,
    ushort ResourceType,
    ushort NamedPipeStatus,
    bool IsDirectory);
