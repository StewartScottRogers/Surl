using System.Globalization;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Answers GET Object and HEAD Object for an object that exists ("GET Object", "HEAD Object";
/// ADR-0110, decisions 1 and 1.1): the generation conditions, then the HTTP conditional fields,
/// then the range, then the object's head - and its bytes unless the request is a <c>HEAD</c>.
/// </summary>
internal static class GcsObjectAnswer
{
    private const string UserMetadataPrefix = "x-goog-meta-";
    private static readonly HttpStatus PartialContent = new(206, "Partial Content");
    private static readonly HttpStatus NotModified = new(304, "Not Modified");

    /// <summary>
    /// Answers a read of an object.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="requestFields">The request's fields, read for the conditions and the range.</param>
    /// <param name="properties">The object's properties.</param>
    /// <param name="digests">The object's digests, for <c>x-goog-hash</c>.</param>
    /// <param name="wholeObject">The object's bytes, as the whole file.</param>
    /// <returns>The answer.</returns>
    public static HttpAnswer Answer(GcsReply reply, IReadOnlyList<KeyValuePair<string, string>> requestFields, ObjectProperties properties, GcsObjectDigests digests, HttpAnswerBody.ContentFile wholeObject)
    {
        if (GcsGenerationConditions.Check(requestFields, properties) is { } generationRefusal)
        {
            return reply.Error(generationRefusal);
        }

        var condition = GcsConditions.Evaluate(requestFields, properties.ETag, properties.LastWriteTimeUtc);
        if (condition == GcsConditionOutcome.PreconditionFailed)
        {
            return reply.Error(GcsError.PreconditionFailed);
        }

        if (condition == GcsConditionOutcome.NotModified)
        {
            return reply.Answer(NotModified, [new("Last-Modified", HttpDate(properties.LastWriteTimeUtc)), new("ETag", properties.ETag)], HttpAnswerBody.None);
        }

        var range = GcsRangeSelection.Select(GcsRequestFields.Value(requestFields, "Range"), properties.Length);

        return range is { IsSatisfiable: false }
            ? reply.Error(GcsError.InvalidRange)
            : ServeObject(reply, properties, digests, wholeObject, range);
    }

    // 200 with the whole object, or 206 with the one range selected; a HEAD without the bytes.
    internal static HttpAnswer ServeObject(GcsReply reply, ObjectProperties properties, GcsObjectDigests digests, HttpAnswerBody.ContentFile wholeObject, ContentByteRange? range)
    {
        var selected = range ?? wholeObject.Range;
        var fields = ObjectFields(properties, digests);
        if (range is not null)
        {
            fields.Add(new("Content-Range", $"bytes {range.FirstByte}-{range.LastByte}/{range.FileLength}"));
        }

        fields.Add(new("Content-Length", selected.ByteCount.ToString(CultureInfo.InvariantCulture)));
        HttpAnswerBody body = reply.IsHead ? HttpAnswerBody.None : wholeObject with { Range = selected };

        return reply.Answer(range is null ? HttpStatus.Ok : PartialContent, fields, body);
    }

    // Decision 1.1's head: the dates and tag, the stored header fields, each x-goog-meta-*, then
    // the x-goog-* fields, x-goog-hash describing the whole object.
    private static List<KeyValuePair<string, string>> ObjectFields(ObjectProperties properties, GcsObjectDigests digests)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("Last-Modified", HttpDate(properties.LastWriteTimeUtc)),
            new("ETag", properties.ETag),
            new("Accept-Ranges", "bytes"),
            new("Content-Type", properties.ContentType),
        };
        AddStored(fields, "Content-Encoding", properties.ContentEncoding);
        AddStored(fields, "Content-Disposition", properties.ContentDisposition);
        AddStored(fields, "Cache-Control", properties.CacheControl);
        AddStored(fields, "Content-Language", properties.ContentLanguage);
        fields.AddRange(properties.UserMetadata.Select(entry => new KeyValuePair<string, string>(UserMetadataPrefix + entry.Key, entry.Value)));
        fields.Add(new("x-goog-generation", properties.Generation.ToString(CultureInfo.InvariantCulture)));
        fields.Add(new("x-goog-metageneration", properties.Metageneration.ToString(CultureInfo.InvariantCulture)));
        fields.Add(new("x-goog-stored-content-encoding", properties.ContentEncoding ?? "identity"));
        fields.Add(new("x-goog-stored-content-length", properties.Length.ToString(CultureInfo.InvariantCulture)));
        fields.Add(new("x-goog-storage-class", "STANDARD"));
        fields.Add(new("x-goog-hash", digests.HashField));

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
