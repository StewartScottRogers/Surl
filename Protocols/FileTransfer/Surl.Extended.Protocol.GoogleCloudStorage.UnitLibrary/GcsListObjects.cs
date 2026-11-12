using System.Globalization;
using System.Text;
using Surl.Content;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Answers List Objects ("GET Bucket (List Objects)"; ADR-0110, decision 6): one page of a
/// bucket's objects as a <c>ListBucketResult</c>, version 1 paged by <c>marker</c> and
/// <c>NextMarker</c>, version 2 (<c>list-type=2</c>) by continuation tokens.
/// </summary>
/// <remarks>
/// A listing is a directory listing in ADR-0006's terms, so it needs <c>--list-directories</c>,
/// which the store's exposure options carry: off, List Objects is <c>403 AccessDenied</c>.
/// </remarks>
/// <param name="store">The content store the bucket is read from.</param>
/// <param name="properties">Reads each listed object's generation and ETag.</param>
internal sealed class GcsListObjects(ContentStore store, GcsObjectPropertiesReader properties)
{
    /// <summary>
    /// Answers a List Objects whose bucket exists.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="target">The request's bucket and query.</param>
    /// <param name="caller">Who lists, and on which scheme.</param>
    /// <param name="cancellationToken">Cancels the walk and hashing.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(GcsReply reply, GcsRequestTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.ListDirectories)
        {
            return reply.Error(GcsError.AccessDenied);
        }

        if (GcsListRequest.Read(target.Parameters) is not { } request)
        {
            return reply.Error(GcsError.InvalidArgument);
        }

        var folderObjects = await properties.ReadFolderObjectsAsync(cancellationToken);
        var page = GcsListPage.Take(new GcsBucketWalk(store, caller, folderObjects).Files(target.RawBucket, request.Prefix, cancellationToken), request);
        var bucket = Uri.UnescapeDataString(target.RawBucket);
        var objects = new List<(string Name, ObjectProperties Properties)>();
        foreach (var file in page.Contents)
        {
            if ((file.FolderObject ?? await properties.ReadAsync(file.Mapping, GcsIndexKey.Of(store, bucket, file.Key), cancellationToken)) is { } found)
            {
                objects.Add((file.Key, found));
            }
        }

        return reply.Xml(Encoding.UTF8.GetBytes(Render(bucket, request, page, objects, caller.AccountName ?? "anonymous")));
    }

    // The ListBucketResult, its elements in decision 6's order, on one line after the declaration.
    private static string Render(string bucket, GcsListRequest request, GcsListPage page, List<(string Name, ObjectProperties Properties)> objects, string owner)
    {
        var xml = new StringBuilder($"{GcsErrorBody.Declaration}<ListBucketResult xmlns=\"{GcsXml.Namespace}\">");
        GcsXml.Append(xml, "Name", bucket);
        GcsXml.Append(xml, "Prefix", Encoded(request.Prefix, request)!);
        AppendPaging(xml, request, objects.Count + page.CommonPrefixes.Count);
        GcsXml.AppendWhenGiven(xml, "Delimiter", Encoded(request.Delimiter, request));
        GcsXml.Append(xml, "IsTruncated", page.IsTruncated ? "true" : "false");
        AppendNextPage(xml, request, page);
        foreach (var (name, properties) in objects)
        {
            AppendContents(xml, Encoded(name, request)!, properties, request.FetchesOwner ? owner : null);
        }

        foreach (var commonPrefix in page.CommonPrefixes)
        {
            xml.Append("<CommonPrefixes>");
            GcsXml.Append(xml, "Prefix", Encoded(commonPrefix, request)!);
            xml.Append("</CommonPrefixes>");
        }

        return xml.Append("</ListBucketResult>").ToString();
    }

    // Marker (version 1), or ContinuationToken, StartAfter and KeyCount (version 2); then MaxKeys.
    private static void AppendPaging(StringBuilder xml, GcsListRequest request, int keyCount)
    {
        if (request.IsVersion2)
        {
            GcsXml.AppendWhenGiven(xml, "ContinuationToken", request.ContinuationToken);
            GcsXml.AppendWhenGiven(xml, "StartAfter", Encoded(request.StartAfter, request));
            GcsXml.Append(xml, "KeyCount", keyCount.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            GcsXml.AppendWhenGiven(xml, "Marker", Encoded(request.Marker, request));
        }

        GcsXml.Append(xml, "MaxKeys", request.MaxKeys.ToString(CultureInfo.InvariantCulture));
    }

    // NextMarker (version 1) or NextContinuationToken (version 2), when the page is truncated.
    private static void AppendNextPage(StringBuilder xml, GcsListRequest request, GcsListPage page)
    {
        if (page.LastName is not { } lastName)
        {
            return;
        }

        if (request.IsVersion2)
        {
            GcsXml.Append(xml, "NextContinuationToken", GcsContinuationToken.Encode(lastName));
        }
        else
        {
            GcsXml.Append(xml, "NextMarker", Encoded(lastName, request)!);
        }
    }

    private static void AppendContents(StringBuilder xml, string name, ObjectProperties properties, string? owner)
    {
        xml.Append("<Contents>");
        GcsXml.Append(xml, "Key", name);
        GcsXml.Append(xml, "Generation", properties.Generation.ToString(CultureInfo.InvariantCulture));
        GcsXml.Append(xml, "MetaGeneration", properties.Metageneration.ToString(CultureInfo.InvariantCulture));
        GcsXml.Append(xml, "LastModified", GcsXml.Time(properties.LastWriteTimeUtc));
        GcsXml.Append(xml, "ETag", properties.ETag);
        GcsXml.Append(xml, "Size", properties.Length.ToString(CultureInfo.InvariantCulture));
        if (owner is not null)
        {
            xml.Append("<Owner>");
            GcsXml.Append(xml, "ID", owner);
            xml.Append("</Owner>");
        }

        xml.Append("</Contents>");
    }

    private static string? Encoded(string? value, GcsListRequest request) =>
        request.EncodesAsUrl && value is not null ? Uri.EscapeDataString(value) : value;
}
