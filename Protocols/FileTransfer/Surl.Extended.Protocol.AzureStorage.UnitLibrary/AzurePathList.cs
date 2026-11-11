using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Answers Path - List ("Path - List"; ADR-0109, decision 5.4): one page of a filesystem's paths,
/// under <c>directory</c> when it is sent, one level or every level as <c>recursive</c> says, as the
/// page's <c>{"paths":[…]}</c>, with <c>x-ms-continuation</c> naming the next page when there is one.
/// </summary>
/// <remarks>
/// A listing needs <c>--list-directories</c>, as List Blobs does (off: <c>403
/// AuthorizationPermissionMismatch</c>). <c>recursive</c> is required (<c>400
/// MissingRequiredQueryParameter</c>); <c>maxResults</c> is 1 to 5000, default and cap 5000, below 1
/// <c>400 OutOfRangeQueryParameterValue</c>; a <c>directory</c> that is no directory is <c>404
/// PathNotFound</c>. <c>continuation</c> is <see cref="AzureListMarker"/>'s base64url of the last
/// sort key returned. <c>beginFrom</c>, a path relative to the folder listed, starts the listing
/// after that path in the walk's order (a directory's contents come after it); a continuation
/// decides over it. <c>upn</c> is accepted and changes nothing: owners are <c>$superuser</c>.
/// </remarks>
/// <param name="store">The content store the filesystem is read from.</param>
/// <param name="properties">Reads each listed file's properties.</param>
internal sealed class AzurePathList(ContentStore store, AzureBlobPropertiesReader properties)
{
    /// <summary>
    /// The most paths one page holds.
    /// </summary>
    public const int MaxResultsCap = 5000;

    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Answers a Path - List on an existing filesystem.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="target">The request's filesystem and query.</param>
    /// <param name="caller">Who lists, and on which scheme.</param>
    /// <param name="cancellationToken">Cancels the walk and hashing.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(AzureReply reply, AzureBlobTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.ListDirectories)
        {
            return reply.Error(AzureRefusal.Of(AzureError.AuthorizationPermissionMismatch));
        }

        if (ReadQuery(target, out var isRecursive, out var maxResults, out var after) is { } refusal)
        {
            return reply.Error(refusal);
        }

        var folderPath = FolderPathOf(target, out var namePrefix);
        if (!IsDirectory(store.MapBucketRequestPathForCaller(folderPath, caller)))
        {
            return reply.Error(AzureRefusal.Of(AzureError.PathNotFound));
        }

        after ??= BeginFromKey(target, folderPath, namePrefix, caller);
        var found = new AzureFilesystemWalk(store, caller).Paths(folderPath, namePrefix, isRecursive, after, cancellationToken).Take(maxResults + 1).ToList();

        return await PageAsync(reply, Uri.UnescapeDataString(target.RawContainer), found, maxResults, cancellationToken);
    }

    // The request path of the folder listed - the filesystem, or its directory parameter's folder -
    // and what each name under it starts with.
    private static string FolderPathOf(AzureBlobTarget target, out string namePrefix)
    {
        var directory = (AzureDataLakeOperationRecognition.Parameter(target, "directory") ?? string.Empty).Trim('/');
        if (directory.Length == 0)
        {
            namePrefix = string.Empty;
            return $"/{target.RawContainer}/";
        }

        namePrefix = $"{directory}/";
        return $"/{target.RawContainer}/{string.Join('/', directory.Split('/').Select(Uri.EscapeDataString))}/";
    }

    // The sort key beginFrom names: its path under the folder listed, and / after a directory's,
    // so the listing starts after it; null when beginFrom is not sent or is empty.
    private string? BeginFromKey(AzureBlobTarget target, string folderPath, string namePrefix, ContentCaller caller)
    {
        var beginFrom = (AzureDataLakeOperationRecognition.Parameter(target, "beginFrom") ?? string.Empty).Trim('/');
        if (beginFrom.Length == 0)
        {
            return null;
        }

        var mapping = store.MapBucketRequestPathForCaller(folderPath + Uri.EscapeDataString(beginFrom).Replace("%2F", "/", StringComparison.Ordinal), caller);

        return IsDirectory(mapping) ? $"{namePrefix}{beginFrom}/" : namePrefix + beginFrom;
    }

    // The first maxResults paths found, and the continuation when more were found.
    private async Task<HttpAnswer> PageAsync(AzureReply reply, string filesystem, List<AzureListedPath> found, int maxResults, CancellationToken cancellationToken)
    {
        var page = found.Take(maxResults).ToList();
        var body = await RenderAsync(filesystem, page, cancellationToken);
        List<KeyValuePair<string, string>> fields = [new("Content-Type", "application/json;charset=utf-8"), new("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture))];
        if (found.Count > maxResults)
        {
            fields.Add(new("x-ms-continuation", AzureListMarker.Encode(page[^1].SortKey)));
        }

        return reply.Answer(HttpStatus.Ok, fields, new HttpAnswerBody.Bytes(body));
    }

    private bool IsDirectory(ContentPathMapping folder) =>
        folder.IsMapped && store.GetEntryKind(folder) == ContentEntryKind.Directory;

    // recursive (required, true or false), maxResults (1 to 5000) and continuation (a marker).
    private static AzureRefusal? ReadQuery(AzureBlobTarget target, out bool isRecursive, out int maxResults, out string? after)
    {
        isRecursive = false;
        maxResults = MaxResultsCap;
        after = null;
        if (RecursiveRefusal(target, out isRecursive) is { } recursiveRefusal)
        {
            return recursiveRefusal;
        }

        if (MaxResultsRefusal(target, out maxResults) is { } maxResultsRefusal)
        {
            return maxResultsRefusal;
        }

        if (AzureDataLakeOperationRecognition.Parameter(target, "continuation") is not { } continuation)
        {
            return null;
        }

        after = AzureListMarker.Decode(continuation);

        return after is null ? AzureRefusal.InvalidQueryParameterValue("continuation", continuation, "The continuation token is not one this server issued.") : null;
    }

    private static AzureRefusal? RecursiveRefusal(AzureBlobTarget target, out bool isRecursive)
    {
        isRecursive = false;
        if (AzureDataLakeOperationRecognition.Parameter(target, "recursive") is not { } recursive)
        {
            return AzureRefusal.MissingRequiredQueryParameter("recursive");
        }

        if (!bool.TryParse(recursive, out isRecursive))
        {
            return AzureRefusal.InvalidQueryParameterValue("recursive", recursive, "The value must be true or false.");
        }

        return null;
    }

    private static AzureRefusal? MaxResultsRefusal(AzureBlobTarget target, out int maxResults)
    {
        maxResults = MaxResultsCap;
        if (AzureDataLakeOperationRecognition.Parameter(target, "maxResults") is not { } text)
        {
            return null;
        }

        if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var asked))
        {
            return AzureRefusal.InvalidQueryParameterValue("maxResults", text, "The value must be a whole number.");
        }

        maxResults = Math.Min(asked, MaxResultsCap);

        return asked < 1 ? AzureRefusal.OutOfRangeQueryParameterValue("maxResults", text, "1") : null;
    }

    // {"paths":[…]}: each entry in decision 5.4's order. A file gone between the walk and the read is left out.
    private async Task<byte[]> RenderAsync(string filesystem, List<AzureListedPath> page, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, WriterOptions))
        {
            json.WriteStartObject();
            json.WriteStartArray("paths");
            foreach (var path in page)
            {
                if (path.IsDirectory)
                {
                    WriteEntry(json, path.Name, isDirectory: true, 0, path.LastModifiedUtc, AzurePathCreate.DirectoryETag(path.LastModifiedUtc));
                }
                else if (await properties.ReadAsync(path.Mapping, AzureIndexKey.Of(store, filesystem, path.Name), cancellationToken) is { } file)
                {
                    WriteEntry(json, path.Name, isDirectory: false, file.Length, file.LastWriteTimeUtc, file.ETag);
                }
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        return buffer.ToArray();
    }

    // isDirectory only for a directory, the page's default for a file being false; the ETag
    // without its quotes, as List Blobs writes Etag.
    private static void WriteEntry(Utf8JsonWriter json, string name, bool isDirectory, long contentLength, DateTimeOffset lastModified, string eTag)
    {
        json.WriteStartObject();
        json.WriteString("name", name);
        if (isDirectory)
        {
            json.WriteBoolean("isDirectory", true);
        }

        json.WriteNumber("contentLength", contentLength);
        json.WriteString("lastModified", lastModified.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture));
        json.WriteString("etag", eTag.Trim('"'));
        json.WriteString("owner", "$superuser");
        json.WriteString("group", "$superuser");
        json.WriteString("permissions", isDirectory ? "rwxr-x---" : "rw-r-----");
        json.WriteEndObject();
    }
}
