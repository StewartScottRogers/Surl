using Surl.Content;

namespace Surl.Protocol.Smb;

/// <summary>
/// A file an <c>SMB_COM_NT_CREATE_ANDX</c> opened for reading (ADR-0073, decision 5): the tree
/// it was opened on, its name for notes, its content store mapping, its length when it was
/// opened, and how many bytes the reads have answered so far.
/// </summary>
/// <param name="TreeId">The TID the file was opened on; a read or close on another TID does not find it.</param>
/// <param name="NoteName">The share and the NT create's name, <c>share\dir\file.txt</c>, as the notes name it.</param>
/// <param name="Mapping">The content store mapping each read copies from.</param>
/// <param name="Length">The file's length when it was opened, the end of file the NT create response announced.</param>
internal sealed record SmbOpenFile(ushort TreeId, string NoteName, ContentPathMapping Mapping, long Length)
{
    /// <summary>How many bytes the reads on this file have answered.</summary>
    public long BytesRead { get; set; }
}
