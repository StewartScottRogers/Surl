namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// One page of a List Objects listing ("GET Bucket (List Objects)"; ADR-0110, decision 6): the
/// names and common prefixes after the name the request resumes after, at most <c>max-keys</c> of
/// them together.
/// </summary>
/// <param name="Contents">The files listed as objects, in name order.</param>
/// <param name="CommonPrefixes">The common prefixes, each once, in order.</param>
/// <param name="LastName">The last name or common prefix returned when more follow - version 1's
/// <c>NextMarker</c>, and what version 2's token resumes after - or <see langword="null"/> when
/// the page is the last.</param>
internal sealed record GcsListPage(IReadOnlyList<GcsListedFile> Contents, IReadOnlyList<string> CommonPrefixes, string? LastName)
{
    /// <summary>
    /// Whether more names follow this page.
    /// </summary>
    public bool IsTruncated => LastName is not null;

    /// <summary>
    /// Takes one page from a bucket's files.
    /// </summary>
    /// <param name="files">The files whose names start with the request's prefix, in name order.</param>
    /// <param name="request">The request.</param>
    /// <returns>The page.</returns>
    public static GcsListPage Take(IEnumerable<GcsListedFile> files, GcsListRequest request)
    {
        var contents = new List<GcsListedFile>();
        var commonPrefixes = new List<string>();
        string? lastName = null;
        foreach (var (file, name, isCommonPrefix) in Names(files, request))
        {
            if (contents.Count + commonPrefixes.Count == request.MaxKeys)
            {
                return new GcsListPage(contents, commonPrefixes, lastName!);
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

        return new GcsListPage(contents, commonPrefixes, null);
    }

    // Each file's name - its object name, or the common prefix it rolls up into, given once -
    // after the name the request resumes after. A max-keys of 0 names nothing.
    private static IEnumerable<(GcsListedFile File, string Name, bool IsCommonPrefix)> Names(IEnumerable<GcsListedFile> files, GcsListRequest request)
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
        resumesAfter is not null && GcsNameOrder.Instance.Compare(name, resumesAfter) <= 0;

    // The name up to and including the first delimiter after the prefix, or null when there is none.
    private static string? CommonPrefixOf(string name, GcsListRequest request)
    {
        if (request.Delimiter is not { } delimiter)
        {
            return null;
        }

        var at = name.IndexOf(delimiter, request.Prefix.Length, StringComparison.Ordinal);

        return at < 0 ? null : name[..(at + delimiter.Length)];
    }
}
