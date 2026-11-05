namespace Surl.Content;

/// <summary>
/// Where a <see cref="ContentUploadSession"/> commits: the full path of the target, the name its
/// change is published under and asked about at the out-of-space point, and who uploads.
/// </summary>
/// <param name="Location">The target's full path inside the served root or a share.</param>
/// <param name="PublishedName">The request path that reaches the target, a share's name first.</param>
/// <param name="Caller">Who opened the upload, with its scheme.</param>
internal sealed record ContentUploadTarget(string Location, string PublishedName, ContentCaller Caller);
