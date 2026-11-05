namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// One page of a List Blobs listing (ADR-0109, decision 5.3): the blobs and blob prefixes after
/// the name the marker resumes after, in name order, at most <c>maxresults</c> of them together.
/// </summary>
/// <param name="Entries">Each blob, or each blob prefix with no file, in name order.</param>
/// <param name="NextMarker">The marker that resumes after this page, or <see langword="null"/>
/// when the page is the last.</param>
internal sealed record AzureListPage(IReadOnlyList<AzureListPageEntry> Entries, string? NextMarker)
{
    /// <summary>
    /// Takes one page from a container's files.
    /// </summary>
    /// <param name="blobs">The files whose names start with the request's prefix, in name order.</param>
    /// <param name="request">The request.</param>
    /// <returns>The page.</returns>
    public static AzureListPage Take(IEnumerable<AzureListedBlob> blobs, AzureListRequest request)
    {
        var entries = new List<AzureListPageEntry>();
        foreach (var entry in Names(blobs, request))
        {
            if (entries.Count == request.MaxResults)
            {
                return new AzureListPage(entries, AzureListMarker.Encode(entries[^1].Name));
            }

            entries.Add(entry);
        }

        return new AzureListPage(entries, null);
    }

    // Each file's name - its blob name, or the prefix it rolls up into, given once - after the
    // name the marker resumes after.
    private static IEnumerable<AzureListPageEntry> Names(IEnumerable<AzureListedBlob> blobs, AzureListRequest request)
    {
        string? lastBlobPrefix = null;
        foreach (var blob in blobs)
        {
            var blobPrefix = BlobPrefixOf(blob.Name, request);
            if (IsSkipped(blob, blobPrefix, lastBlobPrefix, request.ResumesAfter))
            {
                continue;
            }

            lastBlobPrefix = blobPrefix;
            yield return EntryOf(blob, blobPrefix);
        }
    }

    // A name at or before the one the marker resumes after, or a blob prefix already given.
    private static bool IsSkipped(AzureListedBlob blob, string? blobPrefix, string? lastBlobPrefix, string? resumesAfter) =>
        IsResumedPast(blobPrefix ?? blob.Name, resumesAfter) || (blobPrefix is not null && blobPrefix == lastBlobPrefix);

    private static AzureListPageEntry EntryOf(AzureListedBlob blob, string? blobPrefix) =>
        blobPrefix is null ? new AzureListPageEntry(blob.Name, blob) : new AzureListPageEntry(blobPrefix, null);

    private static bool IsResumedPast(string name, string? resumesAfter) =>
        resumesAfter is not null && AzureNameOrder.Instance.Compare(name, resumesAfter) <= 0;

    // The name up to and including the first delimiter after the prefix, or null when there is none.
    private static string? BlobPrefixOf(string name, AzureListRequest request)
    {
        if (request.Delimiter is not { } delimiter)
        {
            return null;
        }

        var at = name.IndexOf(delimiter, request.Prefix.Length, StringComparison.Ordinal);

        return at < 0 ? null : name[..(at + delimiter.Length)];
    }
}
