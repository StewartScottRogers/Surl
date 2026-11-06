using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Answers Filesystem - List ("Filesystem - List"; ADR-0109, decision 1): one page of the
/// filesystems the caller may read (<see cref="AzureTopLevelFolders"/>) as the page's
/// <c>{"filesystems":[…]}</c>, each with <c>name</c>, <c>lastModified</c> and <c>etag</c>, and
/// <c>x-ms-continuation</c> naming the next page when there is one.
/// </summary>
/// <remarks>
/// A listing needs <c>--list-directories</c> (off: <c>403 AuthorizationPermissionMismatch</c>).
/// <c>prefix</c> limits; <c>maxResults</c> is 1 to 5000, default and cap 5000, below 1 <c>400
/// OutOfRangeQueryParameterValue</c>, no whole number <c>400 InvalidQueryParameterValue</c>;
/// <c>continuation</c> is <see cref="AzureListMarker"/>'s base64url of the last name returned, one
/// this server did not issue <c>400 InvalidQueryParameterValue</c>.
/// </remarks>
/// <param name="store">The content store whose top-level folders are the filesystems.</param>
internal sealed class AzureFilesystemList(ContentStore store)
{
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Answers a Filesystem - List.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="target">The request's query.</param>
    /// <param name="caller">Who lists, and on which scheme.</param>
    /// <param name="cancellationToken">Cancels reading the root folder.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Answer(AzureReply reply, AzureBlobTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.ListDirectories)
        {
            return reply.Error(AzureRefusal.Of(AzureError.AuthorizationPermissionMismatch));
        }

        if (ReadQuery(target, out var maxResults, out var after) is { } refusal)
        {
            return reply.Error(refusal);
        }

        var prefix = AzureDataLakeOperationRecognition.Parameter(target, "prefix") ?? string.Empty;
        var found = new AzureTopLevelFolders(store, caller).Folders(prefix, after, cancellationToken).Take(maxResults + 1).ToList();
        var page = found.Take(maxResults).ToList();
        var body = Render(page);
        List<KeyValuePair<string, string>> fields = [new("Content-Type", "application/json;charset=utf-8"), new("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture))];
        if (found.Count > maxResults)
        {
            fields.Add(new("x-ms-continuation", AzureListMarker.Encode(page[^1].Name)));
        }

        return reply.Answer(HttpStatus.Ok, fields, new HttpAnswerBody.Bytes(body));
    }

    // maxResults (1 to 5000) and continuation (a marker).
    private static AzureRefusal? ReadQuery(AzureBlobTarget target, out int maxResults, out string? after)
    {
        after = null;
        maxResults = AzurePathList.MaxResultsCap;
        if (AzureDataLakeOperationRecognition.Parameter(target, "maxResults") is { } text)
        {
            if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var asked))
            {
                return AzureRefusal.InvalidQueryParameterValue("maxResults", text, "The value must be a whole number.");
            }

            if (asked < 1)
            {
                return AzureRefusal.OutOfRangeQueryParameterValue("maxResults", text, "1");
            }

            maxResults = Math.Min(asked, AzurePathList.MaxResultsCap);
        }

        if (AzureDataLakeOperationRecognition.Parameter(target, "continuation") is not { } continuation)
        {
            return null;
        }

        after = AzureListMarker.Decode(continuation);

        return after is null ? AzureRefusal.InvalidQueryParameterValue("continuation", continuation, "The continuation token is not one this server issued.") : null;
    }

    // {"filesystems":[…]}: each entry's name, lastModified (IMF-fixdate) and etag without its quotes,
    // as Path - List writes them.
    private static byte[] Render(List<AzureTopLevelFolder> page)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, WriterOptions))
        {
            json.WriteStartObject();
            json.WriteStartArray("filesystems");
            foreach (var filesystem in page)
            {
                json.WriteStartObject();
                json.WriteString("name", filesystem.Name);
                json.WriteString("lastModified", filesystem.LastModifiedUtc.ToString("r", CultureInfo.InvariantCulture));
                json.WriteString("etag", AzurePathCreate.DirectoryETag(filesystem.LastModifiedUtc).Trim('"'));
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        return buffer.ToArray();
    }
}
