using System.Globalization;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Control;

/// <summary>
/// Answers the <c>GET</c> requests that read back the mail store, the content store, the message bus, the Redis
/// keyspace, the LDAP directory and the DNS zones (ADR-0164 decisions 7 and 9) by calling the
/// <see cref="IControlTarget"/>. A read changes nothing and publishes nothing.
/// </summary>
/// <param name="target">What the endpoint drives.</param>
/// <param name="answers">Writes the answers.</param>
internal sealed class ControlReadRequests(IControlTarget target, ControlAnswers answers)
{
    /// <summary>The query keys a message listing takes.</summary>
    public static readonly string[] MessageListKeys = ["after_uid", "limit"];

    /// <summary>The query keys a file read or a folder listing takes.</summary>
    public static readonly string[] FileKeys = ["after_name", "limit"];

    /// <summary>The query keys a queue browse takes.</summary>
    public static readonly string[] QueueKeys = ["after_position", "limit"];

    /// <summary>The query keys a log read takes.</summary>
    public static readonly string[] LogKeys = ["from_offset", "limit"];

    /// <summary>The query keys a key read takes.</summary>
    public static readonly string[] KeyKeys = ["database"];

    /// <summary>The query keys a key listing takes.</summary>
    public static readonly string[] KeyListKeys = ["pattern", "after_key", "limit", "database"];

    private const int DefaultLimit = 100;
    private const int MaxLimit = 1000;

    /// <summary><c>GET /v1/mail/{owner}</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>{"mailboxes": [...]}</c>.</returns>
    public async Task<HttpAnswer> ListMailboxesAsync(ControlRequest request)
    {
        var mailboxes = await target.ListMailboxesAsync(request.Parameters[0], request.CancellationToken);
        var body = new ControlMailboxesJson([.. mailboxes.Select(box => new ControlMailboxJson(box.Name, box.MessageCount, box.UnseenCount, box.UidValidity, box.Use))]);

        return answers.Json(request.Http, HttpStatus.Ok, body, ControlJsonContext.Default.ControlMailboxesJson);
    }

    /// <summary><c>GET /v1/mail/{owner}/{mailbox}</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>{"messages": [...], "has_more"}</c>.</returns>
    public async Task<HttpAnswer> ListMessagesAsync(ControlRequest request)
    {
        var afterUid = (uint)request.Query.Number("after_uid", 0, uint.MaxValue, 0);
        var page = await target.ListMessagesAsync(request.Parameters[0], request.Parameters[1], afterUid, Limit(request), request.CancellationToken);
        var body = new ControlMailMessagesJson([.. page.Messages.Select(message => MessageJson(message, null))], page.HasMore);

        return answers.Json(request.Http, HttpStatus.Ok, body, ControlJsonContext.Default.ControlMailMessagesJson);
    }

    /// <summary><c>GET /v1/mail/{owner}/{mailbox}/{uid}</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The message's bytes as <c>message/rfc822</c>, or its JSON when the client accepts <c>application/json</c>.</returns>
    public async Task<HttpAnswer> ReadMessageAsync(ControlRequest request)
    {
        if (!uint.TryParse(request.Parameters[2], NumberStyles.None, CultureInfo.InvariantCulture, out var uid))
        {
            throw new ControlErrorException(ControlError.BadRequest("the UID must be a whole number"));
        }

        var message = await target.ReadMessageAsync(request.Parameters[0], request.Parameters[1], uid, request.CancellationToken);
        if (AcceptsJson(request.Http))
        {
            var shown = ControlShownBytes.Show(message.Bytes.Span, message.Bytes.Length);

            return answers.Json(request.Http, HttpStatus.Ok, MessageJson(message.Summary, shown), ControlJsonContext.Default.ControlMailMessageJson);
        }

        return answers.Bytes(
            request.Http,
            "message/rfc822",
            message.Bytes,
            [new("X-Surl-Uid", message.Summary.Uid.ToString(CultureInfo.InvariantCulture)), new("X-Surl-Flags", string.Join(' ', message.Summary.Flags))]);
    }

    /// <summary><c>GET /v1/files/{path}</c>, or the folder's listing when the path ends in <c>/</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The file's bytes, or <c>{"entries": [...], "has_more"}</c>.</returns>
    public async Task<HttpAnswer> ReadFileOrListFolderAsync(ControlRequest request)
    {
        var rest = request.Parameters[0];
        if (rest.EndsWith('/'))
        {
            return await ListFolderAsync(request, "/" + rest.TrimEnd('/'));
        }

        request.Query.AllowOnly();
        var file = await target.ReadFileAsync("/" + rest, request.CancellationToken);

        return answers.Bytes(
            request.Http,
            file.ContentType,
            file.Bytes,
            [new("Last-Modified", file.ModifiedAt.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture))]);
    }

    /// <summary><c>GET /v1/queues/{name}/messages</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The ready messages, browsed without consuming any.</returns>
    public async Task<HttpAnswer> BrowseQueueAsync(ControlRequest request)
    {
        var after = (int)request.Query.Number("after_position", 0, int.MaxValue - 1, -1);
        var page = await target.BrowseQueueAsync(request.Parameters[0], after, Limit(request), request.CancellationToken);
        var messages = page.Messages.Select(message =>
        {
            var (text, base64) = Show(message.Payload);

            return new ControlQueuedMessageJson(
                message.Position,
                text,
                base64,
                [.. message.Headers.Select(header => new ControlHeaderJson(header.Key, header.Value))]);
        });
        var body = new ControlQueueJson([.. messages], page.ReadyCount, page.InFlightCount, page.HasMore);

        return answers.Json(request.Http, HttpStatus.Ok, body, ControlJsonContext.Default.ControlQueueJson);
    }

    /// <summary><c>GET /v1/topics/{name}/retained</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The retained message, or <c>404</c> when none.</returns>
    public async Task<HttpAnswer> ReadRetainedMessageAsync(ControlRequest request)
    {
        var retained = await target.ReadRetainedMessageAsync(request.Parameters[0], request.CancellationToken);
        var (text, base64) = Show(retained.Payload);
        var body = new ControlRetainedJson(retained.Topic, retained.QualityOfService, text, base64);

        return answers.Json(request.Http, HttpStatus.Ok, body, ControlJsonContext.Default.ControlRetainedJson);
    }

    /// <summary><c>GET /v1/topics/{name}/log</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The records from the offset.</returns>
    public async Task<HttpAnswer> ReadLogAsync(ControlRequest request)
    {
        var fromOffset = request.Query.Number("from_offset", 0, long.MaxValue, 0);
        var page = await target.ReadLogAsync(request.Parameters[0], fromOffset, Limit(request), request.CancellationToken);
        var records = page.Records.Select(record =>
        {
            var (text, base64) = Show(record.Payload);

            return new ControlLogRecordJson(
                record.Offset,
                ControlEventWriter.Timestamp(record.Timestamp),
                record.Key is { } key ? Convert.ToBase64String(key.Span) : null,
                text,
                base64);
        });
        var body = new ControlLogJson([.. records], page.NextOffset, page.HasMore);

        return answers.Json(request.Http, HttpStatus.Ok, body, ControlJsonContext.Default.ControlLogJson);
    }

    /// <summary><c>GET /v1/keys/{key}</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The key with its type, size, expiry and string value.</returns>
    public async Task<HttpAnswer> ReadKeyAsync(ControlRequest request)
    {
        var read = await target.ReadKeyAsync(Database(request), System.Text.Encoding.UTF8.GetBytes(request.Parameters[0]), request.CancellationToken);
        var (text, base64) = read.Value is { } value ? Show(value) : (null, null);

        return answers.Json(request.Http, HttpStatus.Ok, KeyJson(read.Info, text, base64), ControlJsonContext.Default.ControlKeyJson);
    }

    /// <summary><c>GET /v1/keys</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>{"keys": [...], "has_more"}</c>, without values.</returns>
    public async Task<HttpAnswer> ListKeysAsync(ControlRequest request)
    {
        var after = System.Text.Encoding.UTF8.GetBytes(request.Query.Text("after_key") ?? string.Empty);
        var page = await target.ListKeysAsync(Database(request), request.Query.Text("pattern") ?? "*", after, Limit(request), request.CancellationToken);
        var body = new ControlKeysJson([.. page.Keys.Select(key => KeyJson(key, null, null))], page.HasMore);

        return answers.Json(request.Http, HttpStatus.Ok, body, ControlJsonContext.Default.ControlKeysJson);
    }

    /// <summary><c>GET /v1/directory</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The directory as LDIF (<c>text/ldif</c>), <c>userPassword</c> values omitted.</returns>
    public async Task<HttpAnswer> ReadDirectoryAsync(ControlRequest request)
    {
        request.Query.AllowOnly();
        var ldif = await target.ReadDirectoryLdifAsync(request.CancellationToken);

        return answers.Bytes(request.Http, "text/ldif; charset=utf-8", System.Text.Encoding.UTF8.GetBytes(ldif), []);
    }

    /// <summary><c>GET /v1/zones</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>{"zones": [apex, ...]}</c>, each apex with its final dot, in the order the zones were added.</returns>
    public async Task<HttpAnswer> ListZonesAsync(ControlRequest request)
    {
        request.Query.AllowOnly();
        var apexes = await target.ListZonesAsync(request.CancellationToken);

        return answers.Json(request.Http, HttpStatus.Ok, new ControlZonesJson([.. apexes]), ControlJsonContext.Default.ControlZonesJson);
    }

    /// <summary><c>GET /v1/zones/{apex}</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The zone as master-file text (<c>text/dns</c>, RFC 4027).</returns>
    public async Task<HttpAnswer> ReadZoneAsync(ControlRequest request)
    {
        request.Query.AllowOnly();
        var zone = await target.ReadZoneAsync(request.Parameters[0], request.CancellationToken);

        return answers.Bytes(request.Http, "text/dns; charset=utf-8", System.Text.Encoding.UTF8.GetBytes(zone), []);
    }

    private static int Database(ControlRequest request) => (int)request.Query.Number("database", 0, 15, 0);

    private static ControlKeyJson KeyJson(ControlKeyInfo info, string? text, string? base64)
    {
        var (name, nameBase64) = Show(info.Key);

        return new ControlKeyJson(
            name,
            nameBase64,
            info.Type,
            info.ByteCount,
            info.ExpiresAt is { } expiresAt ? ControlEventWriter.Timestamp(expiresAt) : null,
            text,
            base64);
    }

    private async Task<HttpAnswer> ListFolderAsync(ControlRequest request, string path)
    {
        var page = await target.ListFolderAsync(path, request.Query.Text("after_name"), Limit(request), request.CancellationToken);
        var entries = page.Entries.Select(entry => new ControlFolderEntryJson(entry.Name, entry.IsFolder ? "folder" : "file", entry.ByteCount));

        return answers.Json(request.Http, HttpStatus.Ok, new ControlFolderJson([.. entries], page.HasMore), ControlJsonContext.Default.ControlFolderJson);
    }

    private static int Limit(ControlRequest request) => (int)request.Query.Number("limit", 1, MaxLimit, DefaultLimit);

    private static bool AcceptsJson(HttpRequest request) =>
        request.Fields.Any(field => field.Key.Equals("Accept", StringComparison.OrdinalIgnoreCase)
            && field.Value.Contains("application/json", StringComparison.OrdinalIgnoreCase));

    private static (string? Text, string? Base64) Show(ReadOnlyMemory<byte> bytes)
    {
        var shown = ControlShownBytes.Show(bytes.Span, bytes.Length);

        return shown.Encoding == "text" ? (shown.Data, null) : (null, shown.Data);
    }

    private static ControlMailMessageJson MessageJson(ControlMailMessageSummary summary, ControlShownBytes? shown) => new(
        summary.Uid,
        summary.Size,
        summary.Flags,
        ControlEventWriter.Timestamp(summary.InternalDate),
        shown?.Encoding == "text" ? shown.Data : null,
        shown?.Encoding == "base64" ? shown.Data : null);
}
