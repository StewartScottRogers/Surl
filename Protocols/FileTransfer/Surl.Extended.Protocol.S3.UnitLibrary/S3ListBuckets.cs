using System.Globalization;
using System.Text;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Answers ListBuckets (ListBuckets; ADR-0108, decisions 1 and 3; ADR-0102 decision 2.4): the
/// buckets the caller may read as a <c>ListAllMyBucketsResult</c>, in UTF-8 binary order.
/// </summary>
/// <remarks>
/// The buckets are the served root's top-level folders the caller may read - shares included, as
/// the root's listing shows them - and every <c>--bucket</c>, which shadows a top-level folder of
/// its name; a top-level file is not a bucket. Like ListObjectsV2 it is a listing in ADR-0006's
/// sense, so it needs <c>--list-directories</c> (off: <c>403 AccessDenied</c>). A bucket's
/// <c>CreationDate</c> is its folder's last-write time, the only time the store keeps for a folder.
/// </remarks>
/// <param name="store">The content store whose top-level folders are the buckets.</param>
internal sealed class S3ListBuckets(ContentStore store)
{
    private const string Namespace = "http://s3.amazonaws.com/doc/2006-03-01/";

    /// <summary>
    /// Answers a ListBuckets.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="caller">Who lists, and on which scheme.</param>
    /// <param name="cancellationToken">Cancels reading the root folder.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Answer(S3Reply reply, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.ListDirectories)
        {
            return reply.Error(S3Error.AccessDenied);
        }

        var body = Encoding.UTF8.GetBytes(Render(Buckets(caller, cancellationToken), caller.AccountName ?? "anonymous"));

        return reply.Answer(
            HttpStatus.Ok,
            [new("Content-Type", "application/xml"), new("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture))],
            new HttpAnswerBody.Bytes(body));
    }

    // Every name the root lists or a --bucket gives, kept when the caller sees a folder there.
    private List<(string Name, DateTimeOffset Created)> Buckets(ContentCaller caller, CancellationToken cancellationToken)
    {
        var listing = store.ListDirectory(store.MapBucketRequestPathForCaller("/", caller), cancellationToken);
        var buckets = new List<(string Name, DateTimeOffset Created)>();
        var names = listing.Entries.Select(entry => entry.Name).Concat(store.Buckets.Select(bucket => bucket.Name));
        foreach (var name in names.Distinct(StringComparer.Ordinal).Order(S3KeyOrder.Instance))
        {
            // The store lists only names it maps and checks every bucket name at start, so each
            // name maps; with the trailing '/', a status is a folder's, and a file, a hidden share
            // or nothing has none.
            var mapping = store.MapBucketRequestPathForCaller($"/{Uri.EscapeDataString(name)}/", caller);
            if (store.GetEntryStatus(mapping) is { } folder)
            {
                buckets.Add((name, folder.LastModifiedUtc));
            }
        }

        return buckets;
    }

    // The ListAllMyBucketsResult in the order of the API Reference's example: the buckets, each
    // CreationDate then Name, then the owner, DisplayName then ID.
    private static string Render(List<(string Name, DateTimeOffset Created)> buckets, string owner)
    {
        var xml = new StringBuilder($"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<ListAllMyBucketsResult xmlns=\"{Namespace}\"><Buckets>");
        foreach (var (name, created) in buckets)
        {
            xml.Append("<Bucket>");
            S3XmlElement.Append(xml, "CreationDate", created.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
            S3XmlElement.Append(xml, "Name", name);
            xml.Append("</Bucket>");
        }

        xml.Append("</Buckets><Owner>");
        S3XmlElement.Append(xml, "DisplayName", owner);
        S3XmlElement.Append(xml, "ID", owner);

        return xml.Append("</Owner></ListAllMyBucketsResult>").ToString();
    }
}
