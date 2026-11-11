namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// One bucket a caller may read: a top-level folder, or a <c>--bucket</c> name's content path.
/// </summary>
/// <param name="Name">The bucket's name.</param>
/// <param name="LastModifiedUtc">The folder's last-write time, which stands for its creation time too, the store keeping none.</param>
internal sealed record GcsBucketEntry(string Name, DateTimeOffset LastModifiedUtc);
