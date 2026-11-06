namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// One entry of a List Blobs page: a blob, or a <c>BlobPrefix</c> that several blobs roll up into.
/// </summary>
/// <param name="Name">The blob name, or the blob prefix, decoded.</param>
/// <param name="Blob">The file listed, or <see langword="null"/> for a blob prefix.</param>
internal sealed record AzureListPageEntry(string Name, AzureListedBlob? Blob);
