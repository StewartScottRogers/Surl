using System.Text;
using Surl.Content;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Answers List Buckets ("GET Service"; ADR-0110, decision 6): a <c>ListAllMyBucketsResult</c>
/// with the caller as <c>Owner</c> and one <c>Bucket</c> for each top-level folder - or
/// <c>--bucket</c> name - the caller may read, in UTF-8 byte order.
/// </summary>
/// <remarks>
/// A listing is a directory listing in ADR-0006's terms, so it needs <c>--list-directories</c>:
/// off, List Buckets is <c>403 AccessDenied</c>. A folder's <c>CreationDate</c> is its last-write
/// time, the store keeping no creation time.
/// </remarks>
/// <param name="store">The content store whose top-level folders are the buckets.</param>
internal sealed class GcsListBuckets(ContentStore store)
{
    /// <summary>
    /// Answers a List Buckets.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="caller">Who lists, and on which scheme.</param>
    /// <param name="cancellationToken">Cancels listing the root.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Answer(GcsReply reply, ContentCaller caller, CancellationToken cancellationToken)
    {
        var root = store.ListDirectory(store.MapBucketRequestPathForCaller("/", caller), cancellationToken);
        if (!root.IsListed)
        {
            return reply.Error(GcsError.AccessDenied);
        }

        var names = root.Entries.Where(entry => entry.Kind == ContentEntryKind.Directory).Select(entry => entry.Name)
            .Concat(store.Buckets.Select(bucket => bucket.Name))
            .Distinct(StringComparer.Ordinal)
            .Order(GcsNameOrder.Instance);
        var xml = new StringBuilder($"{GcsErrorBody.Declaration}<ListAllMyBucketsResult xmlns=\"{GcsXml.Namespace}\"><Owner>");
        GcsXml.Append(xml, "ID", caller.AccountName ?? "anonymous");
        xml.Append("</Owner><Buckets>");
        foreach (var name in names)
        {
            var folder = store.MapBucketRequestPathForCaller($"/{Uri.EscapeDataString(name)}/", caller);
            if (folder.IsMapped && store.GetEntryStatus(folder) is { Kind: ContentEntryKind.Directory } status)
            {
                xml.Append("<Bucket>");
                GcsXml.Append(xml, "Name", name);
                GcsXml.Append(xml, "CreationDate", GcsXml.Time(status.LastModifiedUtc));
                xml.Append("</Bucket>");
            }
        }

        return reply.Xml(Encoding.UTF8.GetBytes(xml.Append("</Buckets></ListAllMyBucketsResult>").ToString()));
    }
}
