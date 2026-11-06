using Surl.Content;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// One file a bucket walk found: its key and its mapped location.
/// </summary>
/// <param name="Key">The key, decoded: the file's path inside the bucket's folder.</param>
/// <param name="Mapping">The file's mapped location.</param>
internal sealed record GcsListedFile(string Key, ContentPathMapping Mapping);
