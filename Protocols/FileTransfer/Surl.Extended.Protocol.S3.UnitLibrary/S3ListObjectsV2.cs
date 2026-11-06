using System.Globalization;
using System.Text;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Answers ListObjectsV2 (ListObjectsV2; ADR-0108, decision 6): one page of a bucket's keys as a
/// <c>ListBucketResult</c>.
/// </summary>
/// <remarks>
/// A listing is a directory listing in ADR-0006's terms, so it needs <c>--list-directories</c>,
/// which the store's exposure options carry: off, ListObjectsV2 is <c>403 AccessDenied</c>.
/// </remarks>
/// <param name="store">The content store the bucket is read from.</param>
/// <param name="properties">Reads each listed object's ETag.</param>
internal sealed class S3ListObjectsV2(ContentStore store, S3ObjectPropertiesReader properties)
{
    private const string Namespace = "http://s3.amazonaws.com/doc/2006-03-01/";

    /// <summary>
    /// Answers a ListObjectsV2 whose bucket exists.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="target">The request's bucket and query.</param>
    /// <param name="parameters">The query's parameters.</param>
    /// <param name="caller">Who lists, and on which scheme.</param>
    /// <param name="cancellationToken">Cancels the walk and hashing.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(S3Reply reply, S3RequestTarget target, IReadOnlyList<KeyValuePair<string, string>> parameters, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.ListDirectories)
        {
            return reply.Error(S3Error.AccessDenied);
        }

        if (S3ListRequest.Read(parameters, out var refusal) is not { } request)
        {
            return reply.Error(refusal!.Error, refusal.ExtraElements);
        }

        var page = S3ListPage.Take(new S3BucketWalk(store, caller).Files(target.RawBucket, request.Prefix, cancellationToken), request);
        var bucket = Uri.UnescapeDataString(target.RawBucket);
        var objects = new List<(string Key, ObjectProperties Properties)>();
        foreach (var file in page.Contents)
        {
            if (await properties.ReadAsync(file.Mapping, S3IndexKey.Of(store, bucket, file.Key), cancellationToken) is { } found)
            {
                objects.Add((file.Key, found));
            }
        }

        var body = Encoding.UTF8.GetBytes(Render(bucket, request, page, objects, caller.AccountName ?? "anonymous"));

        return reply.Answer(
            HttpStatus.Ok,
            [new("Content-Type", "application/xml"), new("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture))],
            new HttpAnswerBody.Bytes(body));
    }

    // The ListBucketResult, its elements in the page's order, on one line after the declaration.
    private static string Render(string bucket, S3ListRequest request, S3ListPage page, List<(string Key, ObjectProperties Properties)> objects, string owner)
    {
        var xml = new StringBuilder($"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<ListBucketResult xmlns=\"{Namespace}\">");
        Append(xml, "Name", bucket);
        Append(xml, "Prefix", Encoded(request.Prefix, request));
        AppendWhenGiven(xml, "Delimiter", request.Delimiter, request);
        Append(xml, "MaxKeys", request.MaxKeys.ToString(CultureInfo.InvariantCulture));
        Append(xml, "KeyCount", (objects.Count + page.CommonPrefixes.Count).ToString(CultureInfo.InvariantCulture));
        Append(xml, "IsTruncated", page.IsTruncated ? "true" : "false");
        AppendRaw(xml, "ContinuationToken", request.ContinuationToken);
        AppendRaw(xml, "NextContinuationToken", page.NextContinuationToken);
        AppendWhenGiven(xml, "StartAfter", request.StartAfter, request);
        AppendRaw(xml, "EncodingType", request.EncodesAsUrl ? "url" : null);
        foreach (var (key, properties) in objects)
        {
            AppendContents(xml, Encoded(key, request), properties, request.FetchesOwner ? owner : null);
        }

        foreach (var commonPrefix in page.CommonPrefixes)
        {
            xml.Append("<CommonPrefixes>");
            Append(xml, "Prefix", Encoded(commonPrefix, request));
            xml.Append("</CommonPrefixes>");
        }

        return xml.Append("</ListBucketResult>").ToString();
    }

    private static void AppendContents(StringBuilder xml, string key, ObjectProperties properties, string? owner)
    {
        xml.Append("<Contents>");
        Append(xml, "Key", key);
        Append(xml, "LastModified", properties.LastWriteTimeUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
        Append(xml, "ETag", properties.ETag);
        Append(xml, "Size", properties.Length.ToString(CultureInfo.InvariantCulture));
        Append(xml, "StorageClass", "STANDARD");
        if (owner is not null)
        {
            xml.Append("<Owner>");
            Append(xml, "ID", owner);
            Append(xml, "DisplayName", owner);
            xml.Append("</Owner>");
        }

        xml.Append("</Contents>");
    }

    private static string Encoded(string value, S3ListRequest request) =>
        request.EncodesAsUrl ? Uri.EscapeDataString(value) : value;

    private static void AppendWhenGiven(StringBuilder xml, string name, string? value, S3ListRequest request) =>
        AppendRaw(xml, name, value is null ? null : Encoded(value, request));

    private static void AppendRaw(StringBuilder xml, string name, string? value)
    {
        if (value is not null)
        {
            Append(xml, name, value);
        }
    }

    private static void Append(StringBuilder xml, string name, string text) =>
        S3XmlElement.Append(xml, name, text);
}
