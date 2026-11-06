using System.Globalization;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Answers GetObject and HeadObject for an object that exists (GetObject, HeadObject; ADR-0108,
/// decision 1): the conditional fields first, then the range, then the object's head - and its
/// bytes unless the request is a <c>HEAD</c>.
/// </summary>
internal static class S3ObjectAnswer
{
    private static readonly HttpStatus PartialContent = new(206, "Partial Content");
    private static readonly HttpStatus NotModified = new(304, "Not Modified");

    /// <summary>
    /// Answers a read of an object.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="requestFields">The request's fields, read for the conditions and the range.</param>
    /// <param name="properties">The object's properties.</param>
    /// <param name="wholeObject">The object's bytes, as the whole file.</param>
    /// <returns>The answer.</returns>
    public static HttpAnswer Answer(S3Reply reply, IReadOnlyList<KeyValuePair<string, string>> requestFields, ObjectProperties properties, HttpAnswerBody.ContentFile wholeObject)
    {
        var condition = S3Conditions.Evaluate(requestFields, properties.ETag, properties.LastWriteTimeUtc);
        if (condition == S3ConditionOutcome.PreconditionFailed)
        {
            return reply.Error(S3Error.PreconditionFailed);
        }

        if (condition == S3ConditionOutcome.NotModified)
        {
            return reply.Answer(NotModified, [new("Last-Modified", HttpDate(properties.LastWriteTimeUtc)), new("ETag", properties.ETag)], HttpAnswerBody.None);
        }

        var range = S3RangeSelection.Select(S3RequestFields.Value(requestFields, "Range"), properties.Length);

        return range is { IsSatisfiable: false }
            ? reply.Error(S3Error.InvalidRange)
            : ObjectAnswer(reply, properties, wholeObject, range);
    }

    // 200 with the whole object, or 206 with the one range selected; a HEAD without the bytes.
    private static HttpAnswer ObjectAnswer(S3Reply reply, ObjectProperties properties, HttpAnswerBody.ContentFile wholeObject, ContentByteRange? range)
    {
        var selected = range ?? wholeObject.Range;
        var fields = ObjectFields(properties);
        if (range is not null)
        {
            fields.Add(new("Content-Range", $"bytes {range.FirstByte}-{range.LastByte}/{range.FileLength}"));
        }

        fields.Add(new("Content-Length", selected.ByteCount.ToString(CultureInfo.InvariantCulture)));
        HttpAnswerBody body = reply.IsHead ? HttpAnswerBody.None : wholeObject with { Range = selected };

        return reply.Answer(range is null ? HttpStatus.Ok : PartialContent, fields, body);
    }

    // GetObject's head: the dates and tag, the stored header fields and each x-amz-meta-*.
    private static List<KeyValuePair<string, string>> ObjectFields(ObjectProperties properties)
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
        fields.AddRange(properties.UserMetadata.Select(entry => new KeyValuePair<string, string>($"x-amz-meta-{entry.Key}", entry.Value)));

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
