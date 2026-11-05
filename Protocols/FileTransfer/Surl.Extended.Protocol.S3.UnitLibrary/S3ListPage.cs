namespace Surl.Extended.Protocol.S3;

/// <summary>
/// One page of a ListObjectsV2 listing (ListObjectsV2; ADR-0108, decision 6): the keys and common
/// prefixes after the name the request resumes after, at most <c>max-keys</c> of them together.
/// </summary>
/// <param name="Contents">The files listed as keys, in key order.</param>
/// <param name="CommonPrefixes">The common prefixes, each once, in order.</param>
/// <param name="NextContinuationToken">The token that resumes after this page, or
/// <see langword="null"/> when the page is the last.</param>
internal sealed record S3ListPage(IReadOnlyList<S3ListedFile> Contents, IReadOnlyList<string> CommonPrefixes, string? NextContinuationToken)
{
    /// <summary>
    /// Whether more keys follow this page.
    /// </summary>
    public bool IsTruncated => NextContinuationToken is not null;

    /// <summary>
    /// Takes one page from a bucket's files.
    /// </summary>
    /// <param name="files">The files whose keys start with the request's prefix, in key order.</param>
    /// <param name="request">The request.</param>
    /// <returns>The page.</returns>
    public static S3ListPage Take(IEnumerable<S3ListedFile> files, S3ListRequest request)
    {
        var contents = new List<S3ListedFile>();
        var commonPrefixes = new List<string>();
        string? lastName = null;
        foreach (var (file, name, isCommonPrefix) in Names(files, request))
        {
            if (contents.Count + commonPrefixes.Count == request.MaxKeys)
            {
                return new S3ListPage(contents, commonPrefixes, S3ContinuationToken.Encode(lastName!));
            }

            if (isCommonPrefix)
            {
                commonPrefixes.Add(name);
            }
            else
            {
                contents.Add(file);
            }

            lastName = name;
        }

        return new S3ListPage(contents, commonPrefixes, null);
    }

    // Each file's name - its key, or the common prefix it rolls up into, given once - after the
    // name the request resumes after. A max-keys of 0 names nothing.
    private static IEnumerable<(S3ListedFile File, string Name, bool IsCommonPrefix)> Names(IEnumerable<S3ListedFile> files, S3ListRequest request)
    {
        string? lastCommonPrefix = null;
        foreach (var file in request.MaxKeys == 0 ? [] : files)
        {
            var commonPrefix = CommonPrefixOf(file.Key, request);
            var name = commonPrefix ?? file.Key;
            if (IsResumedPast(name, request.ResumesAfter) || (commonPrefix is not null && commonPrefix == lastCommonPrefix))
            {
                continue;
            }

            lastCommonPrefix = commonPrefix;
            yield return (file, name, commonPrefix is not null);
        }
    }

    private static bool IsResumedPast(string name, string? resumesAfter) =>
        resumesAfter is not null && S3KeyOrder.Instance.Compare(name, resumesAfter) <= 0;

    // The key up to and including the first delimiter after the prefix, or null when there is none.
    private static string? CommonPrefixOf(string key, S3ListRequest request)
    {
        if (request.Delimiter is not { } delimiter)
        {
            return null;
        }

        var at = key.IndexOf(delimiter, request.Prefix.Length, StringComparison.Ordinal);

        return at < 0 ? null : key[..(at + delimiter.Length)];
    }
}
