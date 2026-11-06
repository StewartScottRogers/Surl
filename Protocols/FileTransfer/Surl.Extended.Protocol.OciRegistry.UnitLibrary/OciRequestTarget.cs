namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// A request target read as the distribution specification's endpoints (ADR-0115, decision 1):
/// the path is percent-decoded once, and <c>&lt;name&gt;</c> is everything between <c>/v2/</c> and
/// the last <c>/blobs/</c>, <c>/manifests/</c> or <c>/referrers/</c> segment whose reference is
/// one segment, before a final <c>/tags/list</c>, or before the last <c>/blobs/uploads/</c>
/// followed by at most one segment, the upload session's reference.
/// </summary>
/// <param name="Endpoint">The endpoint named.</param>
/// <param name="Name">The repository name, percent-decoded; empty for <c>/v2/</c> and no endpoint.</param>
/// <param name="Reference">The digest or tag; empty when the endpoint takes none.</param>
/// <param name="Query">The query after the <c>?</c>, still percent-encoded, or empty.</param>
internal sealed record OciRequestTarget(OciEndpoint Endpoint, string Name, string Reference, string Query)
{
    private const string VersionRoot = "/v2/";
    private const string TagsListSuffix = "/tags/list";
    private const string UploadsMarker = "/blobs/uploads/";

    private static readonly (string Marker, OciEndpoint Endpoint)[] ReferenceMarkers =
    [
        ("/manifests/", OciEndpoint.Manifest),
        ("/blobs/", OciEndpoint.Blob),
        ("/referrers/", OciEndpoint.Referrers),
    ];

    /// <summary>
    /// Reads a request target.
    /// </summary>
    /// <param name="target">The request target as the client sent it.</param>
    /// <returns>The endpoint and its parts; <see cref="OciEndpoint.None"/> when no endpoint matches.</returns>
    public static OciRequestTarget Parse(string target)
    {
        var queryStart = target.IndexOf('?');
        var query = queryStart < 0 ? string.Empty : target[(queryStart + 1)..];
        var path = Uri.UnescapeDataString(queryStart < 0 ? target : target[..queryStart]);
        if (path == VersionRoot)
        {
            return new(OciEndpoint.Base, string.Empty, string.Empty, query);
        }

        return path.StartsWith(VersionRoot, StringComparison.Ordinal)
            ? ParseUnderRoot(path[VersionRoot.Length..], query)
            : new(OciEndpoint.None, string.Empty, string.Empty, query);
    }

    private static OciRequestTarget ParseUnderRoot(string rest, string query)
    {
        if (rest.EndsWith(TagsListSuffix, StringComparison.Ordinal))
        {
            return new(OciEndpoint.TagsList, rest[..^TagsListSuffix.Length], string.Empty, query);
        }

        var uploadsAt = rest.LastIndexOf(UploadsMarker, StringComparison.Ordinal);
        var session = uploadsAt < 0 ? "/" : rest[(uploadsAt + UploadsMarker.Length)..];
        if (!session.Contains('/'))
        {
            return new(session.Length == 0 ? OciEndpoint.Uploads : OciEndpoint.UploadSession, rest[..uploadsAt], session, query);
        }

        foreach (var (marker, endpoint) in ReferenceMarkers)
        {
            var at = rest.LastIndexOf(marker, StringComparison.Ordinal);
            var reference = at < 0 ? string.Empty : rest[(at + marker.Length)..];
            if (reference.Length > 0 && !reference.Contains('/'))
            {
                return new(endpoint, rest[..at], reference, query);
            }
        }

        return new(OciEndpoint.None, string.Empty, string.Empty, query);
    }
}
