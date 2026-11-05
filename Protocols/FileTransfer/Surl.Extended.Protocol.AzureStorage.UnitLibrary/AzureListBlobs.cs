using System.Globalization;
using System.Text;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Answers List Blobs ("List Blobs"; ADR-0109, decision 5.3): one page of a container's blobs and
/// blob prefixes as an <c>EnumerationResults</c>, its elements in the order of the page's example.
/// </summary>
/// <remarks>
/// A listing is a directory listing in ADR-0006's terms, as ADR-0108 holds for S3's, so it needs
/// <c>--list-directories</c>, which the store's exposure options carry: off, List Blobs is
/// <c>403 AuthorizationPermissionMismatch</c>.
/// </remarks>
/// <param name="store">The content store the container is read from.</param>
/// <param name="properties">Reads each listed blob's properties.</param>
/// <param name="scheme">How the endpoint is served, <c>https</c> or <c>http</c>, as
/// <c>ServiceEndpoint</c> names it.</param>
internal sealed class AzureListBlobs(ContentStore store, AzureBlobPropertiesReader properties, string scheme)
{
    /// <summary>
    /// Answers a List Blobs whose container exists.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose <c>Host</c> names the endpoint.</param>
    /// <param name="target">The request's container and query.</param>
    /// <param name="caller">Who lists, and on which scheme.</param>
    /// <param name="cancellationToken">Cancels the walk and hashing.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(AzureReply reply, HttpRequest request, AzureBlobTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.ListDirectories)
        {
            return reply.Error(AzureRefusal.Of(AzureError.AuthorizationPermissionMismatch));
        }

        if (AzureListRequest.Read(target.QueryParameters, out var refusal) is not { } listRequest)
        {
            return reply.Error(refusal!);
        }

        var page = AzureListPage.Take(new AzureContainerWalk(store, caller).Blobs(target.RawContainer, listRequest.Prefix, cancellationToken), listRequest);
        var container = Uri.UnescapeDataString(target.RawContainer);
        var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xml.Append("<EnumerationResults ServiceEndpoint=\"").Append(Escaped($"{scheme}://{AzureRequestFields.Value(request.Fields, "Host")}/"))
            .Append("\" ContainerName=\"").Append(Escaped(container)).Append("\">");
        AppendSent(xml, "Prefix", listRequest.SentPrefix);
        AppendSent(xml, "Marker", listRequest.Marker);
        AppendSent(xml, "MaxResults", listRequest.SentMaxResults ? listRequest.MaxResults.ToString(CultureInfo.InvariantCulture) : null);
        AppendSent(xml, "Delimiter", listRequest.Delimiter);
        xml.Append("<Blobs>");
        foreach (var entry in page.Entries)
        {
            await AppendEntryAsync(xml, container, entry, listRequest.IncludesMetadata, cancellationToken);
        }

        xml.Append("</Blobs>");
        Append(xml, "NextMarker", page.NextMarker);
        var body = Encoding.UTF8.GetBytes(xml.Append("</EnumerationResults>").ToString());

        return reply.Answer(
            HttpStatus.Ok,
            [new("Content-Type", "application/xml"), new("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture))],
            new HttpAnswerBody.Bytes(body));
    }

    // A blob prefix is its Name; a blob is its Name and Properties, and Metadata when asked. A file
    // gone between the walk and the read is left out.
    private async Task AppendEntryAsync(StringBuilder xml, string container, AzureListPageEntry entry, bool includesMetadata, CancellationToken cancellationToken)
    {
        if (entry.Blob is not { } blob)
        {
            xml.Append("<BlobPrefix>");
            Append(xml, "Name", entry.Name);
            xml.Append("</BlobPrefix>");
            return;
        }

        if (await properties.ReadAsync(blob.Mapping, AzureIndexKey.Of(store, container, blob.Name), cancellationToken) is not { } found)
        {
            return;
        }

        xml.Append("<Blob>");
        Append(xml, "Name", blob.Name);
        AppendProperties(xml, found);
        if (includesMetadata)
        {
            AppendMetadata(xml, found.UserMetadata);
        }

        xml.Append("</Blob>");
    }

    // The page example's Properties, in its order; Creation-Time is Last-Modified, the store
    // keeping no creation time, and Etag is the ETag without its quotes, as the example writes it.
    private static void AppendProperties(StringBuilder xml, ObjectProperties properties)
    {
        var lastModified = properties.LastWriteTimeUtc.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);
        xml.Append("<Properties>");
        Append(xml, "Creation-Time", lastModified);
        Append(xml, "Last-Modified", lastModified);
        Append(xml, "Etag", properties.ETag.Trim('"'));
        Append(xml, "Content-Length", properties.Length.ToString(CultureInfo.InvariantCulture));
        Append(xml, "Content-Type", properties.ContentType);
        Append(xml, "Content-Encoding", properties.ContentEncoding);
        Append(xml, "Content-Language", properties.ContentLanguage);
        Append(xml, "Content-MD5", AzureBlobAnswer.StoredMd5(properties.ETag));
        Append(xml, "Cache-Control", properties.CacheControl);
        Append(xml, "Content-Disposition", properties.ContentDisposition);
        Append(xml, "BlobType", "BlockBlob");
        Append(xml, "AccessTier", "Hot");
        Append(xml, "AccessTierInferred", "true");
        Append(xml, "LeaseStatus", "unlocked");
        Append(xml, "LeaseState", "available");
        Append(xml, "ServerEncrypted", "false");
        xml.Append("</Properties>");
    }

    private static void AppendMetadata(StringBuilder xml, IReadOnlyList<KeyValuePair<string, string>> metadata)
    {
        if (metadata.Count == 0)
        {
            xml.Append("<Metadata />");
            return;
        }

        xml.Append("<Metadata>");
        foreach (var entry in metadata)
        {
            Append(xml, entry.Key, entry.Value);
        }

        xml.Append("</Metadata>");
    }

    private static void AppendSent(StringBuilder xml, string name, string? value)
    {
        if (value is not null)
        {
            Append(xml, name, value);
        }
    }

    // An element with its text escaped, or the empty element the page's example writes for no value.
    private static void Append(StringBuilder xml, string name, string? text)
    {
        if (text is null)
        {
            xml.Append('<').Append(name).Append(" />");
            return;
        }

        xml.Append('<').Append(name).Append('>').Append(Escaped(text)).Append("</").Append(name).Append('>');
    }

    private static string Escaped(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
}
