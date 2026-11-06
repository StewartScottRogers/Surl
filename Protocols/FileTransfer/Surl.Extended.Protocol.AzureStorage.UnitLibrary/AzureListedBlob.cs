using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// One file a container walk found: its blob name and its mapped location.
/// </summary>
/// <param name="Name">The blob name, decoded: the file's path inside the container's folder.</param>
/// <param name="Mapping">The file's mapped location.</param>
internal sealed record AzureListedBlob(string Name, ContentPathMapping Mapping);
