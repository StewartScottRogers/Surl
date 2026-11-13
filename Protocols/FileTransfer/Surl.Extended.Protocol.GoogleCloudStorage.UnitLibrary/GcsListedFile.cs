using Surl.Content;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// One object a bucket walk found: a file, or a folder a client put as an object: its key and its
/// mapped location.
/// </summary>
/// <param name="Key">The key, decoded: the file's path inside the bucket's folder, or the folder's
/// path and a final <c>/</c>.</param>
/// <param name="Mapping">The file's or folder's mapped location.</param>
/// <param name="FolderObject">A folder object's properties from the object metadata index, or
/// <see langword="null"/> for a file, whose properties are read when it is listed.</param>
internal sealed record GcsListedFile(string Key, ContentPathMapping Mapping, ObjectProperties? FolderObject = null);
