using System.Text;
using Surl.Content;
using Surl.HttpServing;

namespace Surl.Protocol.Http;

/// <summary>
/// The <c>multipart/byteranges</c> body of a <c>206</c> that answers several ranges (RFC 9110,
/// section 14.6; ADR-0133, Amendment 1): each part is the delimiter <c>--SURL_BYTERANGES</c>, the
/// file's <c>Content-Type</c> and the part's <c>Content-Range</c>, an empty line and the range's
/// bytes; CRLF, the close delimiter <c>--SURL_BYTERANGES--</c> and CRLF end it.
/// </summary>
internal static class HttpByteRangesBody
{
    /// <summary>
    /// The boundary between parts: fixed, so every answer is the same bytes every run.
    /// </summary>
    public const string Boundary = "SURL_BYTERANGES";

    /// <summary>
    /// The answer's <c>Content-Type</c>, naming <see cref="Boundary"/>.
    /// </summary>
    public const string ContentType = "multipart/byteranges; boundary=" + Boundary;

    /// <summary>
    /// Builds the body for <paramref name="ranges"/> of the file at <paramref name="mapping"/>.
    /// </summary>
    /// <param name="contentStore">The content store the file is copied from.</param>
    /// <param name="mapping">Where the file is.</param>
    /// <param name="mediaType">The file's own media type, sent in every part.</param>
    /// <param name="ranges">The satisfiable ranges, in the order sent.</param>
    /// <returns>The body, ready for any HTTP version's writer.</returns>
    public static HttpAnswerBody.ContentFileParts Of(ContentStore contentStore, ContentPathMapping mapping, string mediaType, IReadOnlyList<ContentByteRange> ranges)
    {
        var parts = ranges
            .Select((range, index) => new ContentFilePart(Latin1(PartPreamble(index == 0, mediaType, range)), range))
            .ToList();

        return new HttpAnswerBody.ContentFileParts(contentStore, mapping, parts, Latin1($"\r\n--{Boundary}--\r\n"));
    }

    // The CRLF that ends the previous part's bytes belongs to the delimiter after them (RFC 2046,
    // section 5.1.1), so every part but the first begins with it.
    private static string PartPreamble(bool isFirst, string mediaType, ContentByteRange range) =>
        FormattableString.Invariant(
            $"{(isFirst ? string.Empty : "\r\n")}--{Boundary}\r\nContent-Type: {mediaType}\r\nContent-Range: bytes {range.FirstByte}-{range.LastByte}/{range.FileLength}\r\n\r\n");

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);
}
