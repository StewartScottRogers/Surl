namespace Surl.Composition;

/// <summary>One child of a folder in the content store.</summary>
/// <param name="Name">The child's name, without its path.</param>
/// <param name="IsFolder">Whether the child is a folder; otherwise it is a file.</param>
/// <param name="ByteCount">A file's size in bytes; <see langword="null"/> for a folder.</param>
public sealed record ComposedFolderEntry(string Name, bool IsFolder, long? ByteCount);
