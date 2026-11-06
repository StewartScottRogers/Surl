using System.Globalization;
using System.Text;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Answers List Containers ("List Containers"; ADR-0109, decision 1): one page of the containers the
/// caller may read (<see cref="AzureTopLevelFolders"/>) as an <c>EnumerationResults</c>, its elements
/// in the order of the page's example.
/// </summary>
/// <remarks>
/// A listing needs <c>--list-directories</c>, as List Blobs does (off: <c>403
/// AuthorizationPermissionMismatch</c>). <c>prefix</c>, <c>marker</c> and <c>maxresults</c> are read
/// as List Blobs reads them (<see cref="AzureListRequest"/>: default and cap 5000, below 1 <c>400
/// OutOfRangeQueryParameterValue</c>, a marker this server did not issue <c>400
/// InvalidQueryParameterValue</c>). <c>include=metadata</c> adds an empty <c>Metadata</c>, because a
/// folder keeps no metadata; <c>deleted</c> and <c>system</c> are accepted and add nothing, because
/// Surl keeps no deleted or system containers; another value is <c>400
/// InvalidQueryParameterValue</c>. A container's <c>Etag</c> is its folder's
/// (<see cref="AzurePathCreate.DirectoryETag"/>), without its quotes, as List Blobs writes it.
/// </remarks>
/// <param name="store">The content store whose top-level folders are the containers.</param>
/// <param name="scheme">How the endpoint is served, <c>https</c> or <c>http</c>, as
/// <c>ServiceEndpoint</c> names it.</param>
internal sealed class AzureListContainers(ContentStore store, string scheme)
{
    private static readonly string[] IncludeValues = ["metadata", "deleted", "system"];

    /// <summary>
    /// Answers a List Containers.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose <c>Host</c> names the endpoint.</param>
    /// <param name="target">The request's query.</param>
    /// <param name="caller">Who lists, and on which scheme.</param>
    /// <param name="cancellationToken">Cancels reading the root folder.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Answer(AzureReply reply, HttpRequest request, AzureBlobTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.ListDirectories)
        {
            return reply.Error(AzureRefusal.Of(AzureError.AuthorizationPermissionMismatch));
        }

        var include = AzureDataLakeOperationRecognition.Parameter(target, "include") ?? string.Empty;
        var includes = include.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (includes.FirstOrDefault(value => !IncludeValues.Contains(value)) is { } unknown)
        {
            return reply.Error(AzureRefusal.InvalidQueryParameterValue("include", unknown, "The value is not one of the values List Containers takes."));
        }

        var withoutInclude = target.QueryParameters.Where(parameter => !parameter.Key.Equals("include", StringComparison.OrdinalIgnoreCase)).ToList();
        if (AzureListRequest.Read(withoutInclude, out var refusal) is not { } listRequest)
        {
            return reply.Error(refusal!);
        }

        var found = new AzureTopLevelFolders(store, caller).Folders(listRequest.Prefix, listRequest.ResumesAfter, cancellationToken).Take(listRequest.MaxResults + 1).ToList();
        var body = Encoding.UTF8.GetBytes(Render(AzureRequestFields.Value(request.Fields, "Host"), listRequest, found, includes.Contains("metadata")));

        return reply.Answer(
            HttpStatus.Ok,
            [new("Content-Type", "application/xml"), new("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture))],
            new HttpAnswerBody.Bytes(body));
    }

    // The EnumerationResults: the sent Prefix, Marker and MaxResults, each container on the page,
    // then NextMarker, empty on the last page.
    private string Render(string? host, AzureListRequest listRequest, List<AzureTopLevelFolder> found, bool includesMetadata)
    {
        var page = found.Take(listRequest.MaxResults).ToList();
        var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xml.Append("<EnumerationResults ServiceEndpoint=\"").Append(AzureListBlobs.Escaped($"{scheme}://{host}/")).Append("\">");
        AzureListBlobs.AppendSent(xml, "Prefix", listRequest.SentPrefix);
        AzureListBlobs.AppendSent(xml, "Marker", listRequest.Marker);
        AzureListBlobs.AppendSent(xml, "MaxResults", listRequest.SentMaxResults ? listRequest.MaxResults.ToString(CultureInfo.InvariantCulture) : null);
        xml.Append("<Containers>");
        foreach (var container in page)
        {
            AppendContainer(xml, container, includesMetadata);
        }

        xml.Append("</Containers>");
        AzureListBlobs.Append(xml, "NextMarker", found.Count > page.Count ? AzureListMarker.Encode(page[^1].Name) : null);

        return xml.Append("</EnumerationResults>").ToString();
    }

    // The page example's Container: Name, then Properties in its order, then Metadata when asked.
    private static void AppendContainer(StringBuilder xml, AzureTopLevelFolder container, bool includesMetadata)
    {
        xml.Append("<Container>");
        AzureListBlobs.Append(xml, "Name", container.Name);
        xml.Append("<Properties>");
        AzureListBlobs.Append(xml, "Last-Modified", container.LastModifiedUtc.ToString("r", CultureInfo.InvariantCulture));
        AzureListBlobs.Append(xml, "Etag", AzurePathCreate.DirectoryETag(container.LastModifiedUtc).Trim('"'));
        AzureListBlobs.Append(xml, "LeaseStatus", "unlocked");
        AzureListBlobs.Append(xml, "LeaseState", "available");
        AzureListBlobs.Append(xml, "HasImmutabilityPolicy", "false");
        AzureListBlobs.Append(xml, "HasLegalHold", "false");
        xml.Append("</Properties>");
        if (includesMetadata)
        {
            xml.Append("<Metadata />");
        }

        xml.Append("</Container>");
    }
}
