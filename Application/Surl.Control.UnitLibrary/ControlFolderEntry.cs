namespace Surl.Control;

/// <summary>One child of a content-store folder.</summary>
/// <param name="Name">The child's name.</param>
/// <param name="IsFolder">Whether it is a folder rather than a file.</param>
/// <param name="ByteCount">A file's size, or <see langword="null"/> for a folder.</param>
public sealed record ControlFolderEntry(string Name, bool IsFolder, long? ByteCount);
