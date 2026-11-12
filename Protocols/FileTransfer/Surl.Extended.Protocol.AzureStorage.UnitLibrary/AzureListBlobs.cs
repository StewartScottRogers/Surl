using System.Globalization;
using System.Text;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Answers List Blobs ("List Blobs"; ADR-0109, decision 5.3): one page of a container's blobs and
/// blob prefixes as an <c>EnumerationResults</c>, its elements in the order of the page's example.
/// With <c>include=uncommittedblobs</c> a blob that has only blocks staged by Put Block is listed
/// too, in name order among the files, with a <c>Content-Length</c> of 0.
/// </summary>
/// <remarks>
/// A listing is a directory listing in ADR-0006's terms, as ADR-0108 holds for S3's, so it needs
/// <c>--list-directories</c>, which the store's exposure options carry: off, List Blobs is
/// <c>403 AuthorizationPermissionMismatch</c>.
/// </remarks>
/// <param name="store">The content store the container is read from.</param>
/// <param name="properties">Reads each listed blob's properties.</param>
/// <param name="staging">The server's staged uploads, whose blobs with only staged blocks
/// <c>include=uncommittedblobs</c> lists.</param>
/// <param name="scheme">How the endpoint is served, <c>https</c> or <c>http</c>, as
/// <c>ServiceEndpoint</c> names it.</param>
internal sealed class AzureListBlobs(ContentStore store, AzureBlobPropertiesReader properties, AzureStagingArea staging, string scheme)
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

        var container = Uri.UnescapeDataString(target.RawContainer);
        var files = new AzureContainerWalk(store, caller).Blobs(target.RawContainer, listRequest.Prefix, cancellationToken);
        var blobs = listRequest.IncludesUncommittedBlobs
            ? Merged(files, await UncommittedBlobsAsync(container, listRequest.Prefix, cancellationToken))
            : files;
        var page = AzureListPage.Take(blobs, listRequest);
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

    // The blobs with only staged blocks, read in turn with the uploads that stage them.
    private async Task<IReadOnlyList<AzureListedBlob>> UncommittedBlobsAsync(string container, string prefix, CancellationToken cancellationToken)
    {
        await staging.EnterAsync(cancellationToken);
        try
        {
            return staging.UncommittedBlobs(container, prefix);
        }
        finally
        {
            staging.Exit();
        }
    }

    /// <summary>
    /// The files and the blobs with staged blocks together, in name order; a name that is both is
    /// the file, its committed blob.
    /// </summary>
    /// <param name="files">The files, in name order.</param>
    /// <param name="uncommitted">The blobs with staged blocks, in name order.</param>
    /// <returns>Every blob once, in name order.</returns>
    internal static IEnumerable<AzureListedBlob> Merged(IEnumerable<AzureListedBlob> files, IReadOnlyList<AzureListedBlob> uncommitted)
    {
        var next = 0;
        foreach (var file in files)
        {
            for (; next < uncommitted.Count && AzureNameOrder.Instance.Compare(uncommitted[next].Name, file.Name) <= 0; next++)
            {
                if (uncommitted[next].Name != file.Name)
                {
                    yield return uncommitted[next];
                }
            }

            yield return file;
        }

        for (; next < uncommitted.Count; next++)
        {
            yield return uncommitted[next];
        }
    }

    // A blob prefix is its Name; a blob is its Name and Properties, and Metadata when asked. A file
    // gone between the walk and the read is left out; a blob with only staged blocks has no
    // committed properties and no metadata.
    private async Task AppendEntryAsync(StringBuilder xml, string container, AzureListPageEntry entry, bool includesMetadata, CancellationToken cancellationToken)
    {
        if (entry.Blob is not { } blob)
        {
            xml.Append("<BlobPrefix>");
            Append(xml, "Name", entry.Name);
            xml.Append("</BlobPrefix>");
            return;
        }

        if (blob.Mapping is not { } mapping)
        {
            AppendBlob(xml, blob.Name, includesMetadata ? [] : null, properties => AppendUncommittedProperties(properties, blob.LastStaged));
            return;
        }

        if (await properties.ReadAsync(mapping, AzureIndexKey.Of(store, container, blob.Name), cancellationToken) is { } found)
        {
            AppendBlob(xml, blob.Name, includesMetadata ? found.UserMetadata : null, properties => AppendProperties(properties, found));
        }
    }

    // A Blob element: its Name, its Properties, and its Metadata when asked for.
    private static void AppendBlob(StringBuilder xml, string name, IReadOnlyList<KeyValuePair<string, string>>? metadata, Action<StringBuilder> appendProperties)
    {
        xml.Append("<Blob>");
        Append(xml, "Name", name);
        appendProperties(xml);
        if (metadata is not null)
        {
            AppendMetadata(xml, metadata);
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
        Append(xml, "Content-MD5", AzureBlobAnswer.StoredMd5(properties));
        Append(xml, "Cache-Control", properties.CacheControl);
        Append(xml, "Content-Disposition", properties.ContentDisposition);
        AppendTail(xml);
    }

    // What every listed blob's Properties end with.
    private static void AppendTail(StringBuilder xml)
    {
        Append(xml, "BlobType", "BlockBlob");
        Append(xml, "AccessTier", "Hot");
        Append(xml, "AccessTierInferred", "true");
        Append(xml, "LeaseStatus", "unlocked");
        Append(xml, "LeaseState", "available");
        Append(xml, "ServerEncrypted", "false");
        xml.Append("</Properties>");
    }

    // The Properties of a blob with only staged blocks, in the same order: no committed content, so
    // Content-Length 0, no Etag and no content fields, and its times the last block's.
    private static void AppendUncommittedProperties(StringBuilder xml, DateTimeOffset lastStaged)
    {
        var lastModified = lastStaged.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);
        xml.Append("<Properties>");
        Append(xml, "Creation-Time", lastModified);
        Append(xml, "Last-Modified", lastModified);
        Append(xml, "Etag", null);
        Append(xml, "Content-Length", "0");
        foreach (var name in (string[])["Content-Type", "Content-Encoding", "Content-Language", "Content-MD5", "Cache-Control", "Content-Disposition"])
        {
            Append(xml, name, null);
        }

        AppendTail(xml);
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

    internal static void AppendSent(StringBuilder xml, string name, string? value)
    {
        if (value is not null)
        {
            Append(xml, name, value);
        }
    }

    // An element with its text escaped, or the empty element the page's example writes for no value.
    internal static void Append(StringBuilder xml, string name, string? text)
    {
        if (text is null)
        {
            xml.Append('<').Append(name).Append(" />");
            return;
        }

        xml.Append('<').Append(name).Append('>').Append(Escaped(text)).Append("</").Append(name).Append('>');
    }

    internal static string Escaped(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
}
