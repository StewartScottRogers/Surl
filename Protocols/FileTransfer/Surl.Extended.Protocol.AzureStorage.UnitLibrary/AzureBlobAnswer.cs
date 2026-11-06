using System.Globalization;
using System.Security.Cryptography;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Answers Get Blob and Get Blob Properties for a blob that exists ("Get Blob", "Get Blob
/// Properties"; ADR-0109, decision 1): the conditional fields first, then the range, then the
/// blob's head - and its bytes unless the request is a <c>HEAD</c>.
/// </summary>
internal static class AzureBlobAnswer
{
    /// <summary>
    /// The largest range <c>x-ms-range-get-content-md5: true</c> may ask about: 4 MiB ("Get Blob").
    /// </summary>
    public const long MaxRangeMd5Bytes = 4 * 1024 * 1024;

    private static readonly HttpStatus PartialContent = new(206, "Partial Content");
    private static readonly HttpStatus NotModified = new(304, "Not Modified");

    // Get Blob's fields naming a block blob with no lease and no server encryption.
    private static readonly KeyValuePair<string, string>[] BlobResourceFields =
    [
        new("x-ms-blob-type", "BlockBlob"),
        new("x-ms-lease-status", "unlocked"),
        new("x-ms-lease-state", "available"),
        new("x-ms-server-encrypted", "false"),
    ];

    /// <summary>
    /// Answers a read of a blob.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="requestFields">The request's fields, read for the conditions and the range.</param>
    /// <param name="properties">The blob's properties.</param>
    /// <param name="wholeBlob">The blob's bytes, as the whole file.</param>
    /// <param name="cancellationToken">Cancels the range's hashing.</param>
    /// <returns>The answer.</returns>
    public static Task<HttpAnswer> AnswerAsync(AzureReply reply, IReadOnlyList<KeyValuePair<string, string>> requestFields, ObjectProperties properties, HttpAnswerBody.ContentFile wholeBlob, CancellationToken cancellationToken) =>
        AnswerAsync(reply, requestFields, properties, wholeBlob, BlobResourceFields, cancellationToken);

    /// <summary>
    /// Answers a read of a blob or a Data Lake file, whose head carries <paramref name="resourceFields"/>
    /// after <c>Accept-Ranges</c>: the blob, lease and encryption fields, or Path - Read's
    /// <c>x-ms-resource-type: file</c> (ADR-0109, decision 1).
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="requestFields">The request's fields, read for the conditions and the range.</param>
    /// <param name="properties">The file's properties.</param>
    /// <param name="wholeBlob">The file's bytes, as the whole file.</param>
    /// <param name="resourceFields">The fields naming what kind of resource the file is.</param>
    /// <param name="cancellationToken">Cancels the range's hashing.</param>
    /// <returns>The answer.</returns>
    public static async Task<HttpAnswer> AnswerAsync(AzureReply reply, IReadOnlyList<KeyValuePair<string, string>> requestFields, ObjectProperties properties, HttpAnswerBody.ContentFile wholeBlob, IReadOnlyList<KeyValuePair<string, string>> resourceFields, CancellationToken cancellationToken)
    {
        if (ConditionAnswer(reply, requestFields, properties) is { } conditionAnswer)
        {
            return conditionAnswer;
        }

        // x-ms-range wins over Range when both are sent ("Specifying the range header").
        var rangeField = AzureRequestFields.Value(requestFields, "x-ms-range") ?? AzureRequestFields.Value(requestFields, "Range");
        var range = AzureRangeSelection.Select(rangeField, properties.Length);
        if (RangeRefusal(requestFields, range) is { } rangeRefusal)
        {
            return reply.Error(rangeRefusal);
        }

        var contentMd5 = range is not null && AsksRangeMd5(requestFields)
            ? await RangeMd5Async(wholeBlob, range, cancellationToken)
            : StoredMd5(properties.ETag);

        return BlobAnswer(reply, properties, contentMd5, wholeBlob, range, resourceFields);
    }

    // 412 ConditionNotMet, or 304 with the tag and date, or null to serve the blob.
    private static HttpAnswer? ConditionAnswer(AzureReply reply, IReadOnlyList<KeyValuePair<string, string>> requestFields, ObjectProperties properties) =>
        AzureConditions.Evaluate(requestFields, properties.ETag, properties.LastWriteTimeUtc) switch
        {
            AzureConditionOutcome.ConditionNotMet => reply.Error(AzureRefusal.Of(AzureError.ConditionNotMet)),
            AzureConditionOutcome.NotModified => reply.Answer(NotModified, [new("Last-Modified", HttpDate(properties.LastWriteTimeUtc)), new("ETag", properties.ETag)], HttpAnswerBody.None),
            _ => null,
        };

    // 416 InvalidRange for a range naming no byte; 400 OutOfRangeInput for a range's MD5 past 4 MiB.
    private static AzureRefusal? RangeRefusal(IReadOnlyList<KeyValuePair<string, string>> requestFields, ContentByteRange? range)
    {
        if (range is null)
        {
            return null;
        }

        if (!range.IsSatisfiable)
        {
            return AzureRefusal.Of(AzureError.InvalidRange);
        }

        return AsksRangeMd5(requestFields) && range.ByteCount > MaxRangeMd5Bytes ? AzureRefusal.Of(AzureError.OutOfRangeInput) : null;
    }

    private static bool AsksRangeMd5(IReadOnlyList<KeyValuePair<string, string>> requestFields) =>
        string.Equals(AzureRequestFields.Value(requestFields, "x-ms-range-get-content-md5"), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The base64 <c>Content-MD5</c> of a blob whose ETag is the quoted hexadecimal MD5 the
    /// object metadata index writes, or null for any other ETag.
    /// </summary>
    /// <param name="eTag">The blob's ETag, quotes included.</param>
    /// <returns>The base64 digest, or null.</returns>
    public static string? StoredMd5(string eTag) =>
        eTag.Length == 34 && eTag[0] == '"' && eTag[^1] == '"' && eTag[1..^1].All(char.IsAsciiHexDigit)
            ? Convert.ToBase64String(Convert.FromHexString(eTag[1..^1]))
            : null;

    private static async Task<string> RangeMd5Async(HttpAnswerBody.ContentFile wholeBlob, ContentByteRange range, CancellationToken cancellationToken)
    {
        using var md5 = MD5.Create();
        await using (var hashing = new CryptoStream(Stream.Null, md5, CryptoStreamMode.Write))
        {
            await wholeBlob.Store.CopyFileBytesAsync(wholeBlob.Mapping, range, hashing, cancellationToken);
        }

        return Convert.ToBase64String(md5.Hash!);
    }

    // 200 with the whole blob, or 206 with the one range selected; a HEAD without the bytes.
    private static HttpAnswer BlobAnswer(AzureReply reply, ObjectProperties properties, string? contentMd5, HttpAnswerBody.ContentFile wholeBlob, ContentByteRange? range, IReadOnlyList<KeyValuePair<string, string>> resourceFields)
    {
        var selected = range ?? wholeBlob.Range;
        var fields = new List<KeyValuePair<string, string>>
        {
            new("Content-Length", selected.ByteCount.ToString(CultureInfo.InvariantCulture)),
            new("Content-Type", properties.ContentType),
        };
        if (range is not null)
        {
            fields.Add(new("Content-Range", $"bytes {range.FirstByte}-{range.LastByte}/{range.FileLength}"));
        }

        AddStored(fields, "Content-MD5", contentMd5);
        fields.AddRange(BlobFields(properties, resourceFields));
        HttpAnswerBody body = reply.IsHead ? HttpAnswerBody.None : wholeBlob with { Range = selected };

        return reply.Answer(range is null ? HttpStatus.Ok : PartialContent, fields, body);
    }

    // Get Blob's head after the entity fields: the tag and date, the blob fields, the stored
    // header fields and each x-ms-meta-*.
    private static List<KeyValuePair<string, string>> BlobFields(ObjectProperties properties, IReadOnlyList<KeyValuePair<string, string>> resourceFields)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("ETag", properties.ETag),
            new("Last-Modified", HttpDate(properties.LastWriteTimeUtc)),
            new("Accept-Ranges", "bytes"),
        };
        fields.AddRange(resourceFields);
        AddStored(fields, "Content-Encoding", properties.ContentEncoding);
        AddStored(fields, "Content-Language", properties.ContentLanguage);
        AddStored(fields, "Content-Disposition", properties.ContentDisposition);
        AddStored(fields, "Cache-Control", properties.CacheControl);
        fields.AddRange(properties.UserMetadata.Select(entry => new KeyValuePair<string, string>($"x-ms-meta-{entry.Key}", entry.Value)));

        return fields;
    }

    private static void AddStored(List<KeyValuePair<string, string>> fields, string name, string? value)
    {
        if (value is not null)
        {
            fields.Add(new(name, value));
        }
    }

    private static string HttpDate(DateTimeOffset time) => time.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);
}
