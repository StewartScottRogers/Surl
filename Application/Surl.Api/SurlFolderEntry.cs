namespace Surl.Api;

/// <summary>One child of a folder, as <see cref="SurlServerFiles.ListAsync"/> reads it.</summary>
/// <param name="Name">The child's name.</param>
/// <param name="IsFolder">Whether it is a folder; otherwise a file.</param>
/// <param name="ByteCount">A file's size; <see langword="null"/> for a folder.</param>
public sealed record SurlFolderEntry(string Name, bool IsFolder, long? ByteCount);
