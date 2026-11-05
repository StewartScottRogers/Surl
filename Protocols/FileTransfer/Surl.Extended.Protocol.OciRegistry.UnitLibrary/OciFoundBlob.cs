using Surl.Content;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// A blob found in a repository, its bytes checked against its digest.
/// </summary>
/// <param name="Mapping">The blob file's mapping in the content store.</param>
/// <param name="Length">Its length in bytes.</param>
internal sealed record OciFoundBlob(ContentPathMapping Mapping, long Length);
