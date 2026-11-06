using System.Globalization;
using System.Text;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Answers Get Block List ("Get Block List"; ADR-0109, decision 1): <c>200</c> with the blob's
/// <c>BlockList</c> - its <c>CommittedBlocks</c>, its <c>UncommittedBlocks</c>, or both, as
/// <c>blocklisttype</c> asks (<c>committed</c> when not sent) - written as the page's example is.
/// </summary>
/// <remarks>
/// Another <c>blocklisttype</c> is <c>400 InvalidQueryParameterValue</c>. A blob with no file and
/// no uncommitted block is <c>404 BlobNotFound</c>. A blob written other than by Put Block List has
/// no committed blocks the server can name, so its list is empty.
/// </remarks>
/// <param name="store">The content store the blob is read from.</param>
/// <param name="properties">Reads the blob's ETag and last-write time.</param>
/// <param name="staging">The server's staged uploads.</param>
internal sealed class AzureGetBlockList(ContentStore store, AzureBlobPropertiesReader properties, AzureStagingArea staging)
{
    /// <summary>
    /// Answers a Get Block List whose container exists and whose name is not too long.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="target">The request's container, blob name and <c>blocklisttype</c>.</param>
    /// <param name="caller">Who reads, and on which scheme.</param>
    /// <param name="cancellationToken">Cancels reading the blob's properties and waiting for the staging area.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(AzureReply reply, AzureBlobTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (ListTypeRefusal(target, out var listType) is { } refusal)
        {
            return reply.Error(refusal);
        }

        var mapping = store.MapBucketRequestPathForCaller(target.Path, caller);
        var found = await ReadPropertiesAsync(target, mapping, cancellationToken);
        var (committed, uncommitted) = await BlocksAsync(target, mapping, cancellationToken);
        if (found is null && uncommitted.Count == 0)
        {
            return reply.Error(AzureRefusal.Of(AzureError.BlobNotFound));
        }

        var body = Render(listType == "uncommitted" ? null : committed, listType == "committed" ? null : uncommitted);

        return reply.Answer(HttpStatus.Ok, FieldsOf(found, body), new HttpAnswerBody.Bytes(body));
    }

    // The blob's date and tag when it exists ("Get Block List", "Response headers"), then the body's fields.
    private static List<KeyValuePair<string, string>> FieldsOf(ObjectProperties? found, byte[] body)
    {
        List<KeyValuePair<string, string>> fields = found is null
            ? []
            : [new("Last-Modified", found.LastWriteTimeUtc.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture)), new("ETag", found.ETag)];
        fields.Add(new("Content-Type", "application/xml"));
        fields.Add(new("x-ms-blob-content-length", (found?.Length ?? 0).ToString(CultureInfo.InvariantCulture)));
        fields.Add(new("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture)));

        return fields;
    }

    // blocklisttype, lower-cased: committed when not sent, else committed, uncommitted or all.
    private static AzureRefusal? ListTypeRefusal(AzureBlobTarget target, out string listType)
    {
        var sent = AzureDataLakeOperationRecognition.Parameter(target, "blocklisttype") ?? "committed";
        listType = sent.ToLowerInvariant();

        return listType is "committed" or "uncommitted" or "all"
            ? null
            : AzureRefusal.InvalidQueryParameterValue("blocklisttype", sent, "The block list type must be committed, uncommitted or all.");
    }

    private async Task<ObjectProperties?> ReadPropertiesAsync(AzureBlobTarget target, ContentPathMapping mapping, CancellationToken cancellationToken) =>
        mapping.IsMapped
            ? await properties.ReadAsync(mapping, AzureIndexKey.OfRaw(store, target.RawContainer, target.RawBlob), cancellationToken)
            : null;

    // A copy of the blob's lists, taken in turn with the writers.
    private async Task<(List<AzureBlock> Committed, List<AzureBlock> Uncommitted)> BlocksAsync(AzureBlobTarget target, ContentPathMapping mapping, CancellationToken cancellationToken)
    {
        await staging.EnterAsync(cancellationToken);
        try
        {
            var blocks = staging.Find<AzureStagedBlocks>(AzureStagedBlocks.KeyOf(target));
            var file = mapping.IsMapped ? store.GetFileStatus(mapping) : null;

            return blocks is null ? ([], []) : ([.. blocks.CommittedFor(file)], [.. blocks.Uncommitted]);
        }
        finally
        {
            staging.Exit();
        }
    }

    /// <summary>
    /// The <c>BlockList</c> document on one line: each list asked for, with each block's
    /// <c>Name</c> and <c>Size</c>.
    /// </summary>
    /// <param name="committed">The committed blocks, or null when not asked for.</param>
    /// <param name="uncommitted">The uncommitted blocks, or null when not asked for.</param>
    /// <returns>The document's UTF-8 bytes.</returns>
    internal static byte[] Render(IReadOnlyList<AzureBlock>? committed, IReadOnlyList<AzureBlock>? uncommitted)
    {
        var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"utf-8\"?><BlockList>");
        AppendList(xml, "CommittedBlocks", committed);
        AppendList(xml, "UncommittedBlocks", uncommitted);
        xml.Append("</BlockList>");

        return Encoding.UTF8.GetBytes(xml.ToString());
    }

    // Block IDs are base64, so they need no escaping.
    private static void AppendList(StringBuilder xml, string name, IReadOnlyList<AzureBlock>? blocks)
    {
        if (blocks is null)
        {
            return;
        }

        xml.Append(CultureInfo.InvariantCulture, $"<{name}>");
        foreach (var block in blocks)
        {
            xml.Append(CultureInfo.InvariantCulture, $"<Block><Name>{block.Id}</Name><Size>{block.Size}</Size></Block>");
        }

        xml.Append(CultureInfo.InvariantCulture, $"</{name}>");
    }
}
