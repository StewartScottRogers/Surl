using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// One blob a List Blobs lists: a file a container walk found, or a blob that has only blocks
/// staged by Put Block and no committed file (<c>include=uncommittedblobs</c>).
/// </summary>
/// <param name="Name">The blob name, decoded: the file's path inside the container's folder.</param>
/// <param name="Mapping">The file's mapped location, or <see langword="null"/> for a blob with
/// only staged blocks.</param>
/// <param name="LastStaged">When a blob with only staged blocks last had one put; unused for a file.</param>
internal sealed record AzureListedBlob(string Name, ContentPathMapping? Mapping, DateTimeOffset LastStaged = default);
