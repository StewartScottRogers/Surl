using System.Globalization;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// The answers to a pull that found its content (Dist "Pull"; ADR-0115, decision 5.2).
/// </summary>
internal static class OciPullAnswers
{
    private static readonly HttpStatus PartialContent = new(206, "Partial Content");
    private static readonly HttpStatus RangeNotSatisfiable = new(416, "Range Not Satisfiable");

    /// <summary>
    /// end-3: <c>200</c>, the stored bytes, <c>Content-Type</c> the descriptor's media type
    /// without parameters, and <c>Docker-Content-Digest</c>. <c>Accept</c> neither converts nor
    /// refuses: the stored manifest is the only representation.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="manifest">The manifest.</param>
    /// <returns>The answer; a <c>HEAD</c> gets the same head and no body.</returns>
    public static HttpAnswer Manifest(OciReply reply, OciFoundManifest manifest) =>
        reply.Bytes(
            HttpStatus.Ok,
            WithoutParameters(manifest.Descriptor.MediaType),
            [new("Docker-Content-Digest", manifest.Digest.ToString())],
            manifest.Bytes);

    /// <summary>
    /// end-2: <c>200</c> with the whole blob, or <c>206</c> with the one range a <c>Range</c>
    /// field selects, or <c>416</c> with <c>Content-Range: bytes */&lt;size&gt;</c> and no body
    /// for an unsatisfiable one.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="store">The content store the blob is copied from.</param>
    /// <param name="digest">The blob's digest.</param>
    /// <param name="blob">The blob.</param>
    /// <param name="rangeField">The request's <c>Range</c> field, or <see langword="null"/>.</param>
    /// <returns>The answer; a <c>HEAD</c> gets the same head and no body.</returns>
    public static HttpAnswer Blob(OciReply reply, ContentStore store, OciDigest digest, OciFoundBlob blob, string? rangeField)
    {
        var range = OciRangeSelection.Select(rangeField, blob.Length);
        if (range is null)
        {
            return BlobBytes(reply, store, digest, blob, HttpStatus.Ok, ContentByteRange.WholeFile(blob.Length), []);
        }

        return range.IsSatisfiable
            ? BlobBytes(reply, store, digest, blob, PartialContent, range, [new("Content-Range", $"bytes {range.FirstByte}-{range.LastByte}/{range.FileLength}")])
            : reply.Answer(RangeNotSatisfiable, [new("Content-Range", $"bytes */{blob.Length}"), new("Content-Length", "0")], HttpAnswerBody.None);
    }

    // The blob's head, then the selected bytes unless the request is a HEAD or selects none.
    private static HttpAnswer BlobBytes(OciReply reply, ContentStore store, OciDigest digest, OciFoundBlob blob, HttpStatus status, ContentByteRange selected, KeyValuePair<string, string>[] rangeFields)
    {
        KeyValuePair<string, string>[] fields =
        [
            new("Content-Type", "application/octet-stream"),
            new("Docker-Content-Digest", digest.ToString()),
            new("Accept-Ranges", "bytes"),
            .. rangeFields,
            new("Content-Length", selected.ByteCount.ToString(CultureInfo.InvariantCulture)),
        ];
        HttpAnswerBody body = reply.IsHead || selected.ByteCount == 0 ? HttpAnswerBody.None : new HttpAnswerBody.ContentFile(store, blob.Mapping, selected);

        return reply.Answer(status, fields, body);
    }

    private static string WithoutParameters(string mediaType)
    {
        var semicolon = mediaType.IndexOf(';');

        return (semicolon < 0 ? mediaType : mediaType[..semicolon]).Trim();
    }
}
