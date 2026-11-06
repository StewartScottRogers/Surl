namespace Surl.Content;

/// <summary>
/// A bucket name mapped onto a content-store path for the object-storage servers
/// (<c>--bucket</c>, ADR-0102 decision 2.4).
/// </summary>
/// <remarks>
/// Only <see cref="ContentStore.MapBucketRequestPath(string, string)"/> reads bucket names; the
/// original file servers never see them. The content path is read with the root's and the
/// shares' rules, so <c>/team/photos</c> lies inside the share <c>team</c>.
/// </remarks>
public sealed class ContentBucket
{
    /// <summary>
    /// Creates a bucket named <paramref name="name"/> whose objects live under
    /// <paramref name="contentPath"/>.
    /// </summary>
    /// <param name="name">One path segment a request path may hold, and not <c>.surl</c>.</param>
    /// <param name="contentPath">A content-store path, starting with <c>/</c>, every segment of
    /// which a request path may hold, percent-encoded as a request path is.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a valid folder name, or
    /// <paramref name="contentPath"/> is not a path a request may name.</exception>
    public ContentBucket(string name, string contentPath)
    {
        ContentStore.ThrowUnlessTopLevelFolderName(name, nameof(name));
        ArgumentNullException.ThrowIfNull(contentPath);
        if (RequestPathSegments.Split(contentPath, out string[] segments) != ContentPathRefusal.None)
        {
            throw new ArgumentException("A bucket's content path must be a path a request may name.", nameof(contentPath));
        }

        Name = name;
        ContentPath = contentPath;
        ContentPathSegments = segments;
    }

    /// <summary>The bucket's name: the first segment of an object-storage request path.</summary>
    public string Name { get; }

    /// <summary>The content-store path the bucket's objects live under, as given.</summary>
    public string ContentPath { get; }

    /// <summary>The decoded segments of <see cref="ContentPath"/>.</summary>
    internal string[] ContentPathSegments { get; }
}
