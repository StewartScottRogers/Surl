using System.Globalization;
using System.Text;
using Surl.HttpMessage;

namespace Surl.Protocol.Http;

/// <summary>
/// Reads the body of a request the HTTP server half answers, as its framing declares
/// (RFC 9112, sections 6 and 7.1), leaving unread what the case's server commands say to
/// leave: the whole body for <c>auth_required</c> and <c>no-expect</c>, the last
/// <c>skip:</c> bytes of a <c>Content-Length</c> body.
/// </summary>
/// <remarks>
/// No <c>100 Continue</c> is ever written: a case that wants one carries it in its reply part,
/// and curl sends the body once its own expect timeout runs out. A body whose framing the
/// server cannot read (another transfer coding, a bad <c>Content-Length</c>) is not read.
/// </remarks>
internal static class HttpUpstreamTestCaseRequestBody
{
    private const int MaxChunkLineBytes = 8192;

    /// <summary>
    /// Reads <paramref name="head"/>'s body from <paramref name="reader"/>.
    /// </summary>
    /// <param name="head">The request.</param>
    /// <param name="commands">The case's server commands.</param>
    /// <param name="reader">The connection's reader, just past the head.</param>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>A task that completes when the bytes to read are read, or the client half-closed first.</returns>
    public static async Task ReadAsync(HttpRequestHead head, HttpUpstreamTestCaseServerCommands commands, HttpConnectionReader reader, CancellationToken cancellationToken)
    {
        var framing = HttpRequestBodyFraming.Of(head);
        if (IsLeftUnread(head, commands))
        {
            return;
        }

        if (framing.Kind == HttpRequestBodyFramingKind.ContentLength)
        {
            var skipped = IsUpload(head) ? commands.SkippedBodyBytes : 0;
            await ReadExactlyAsync(reader, Math.Max(0, framing.ContentLength - skipped), cancellationToken);
        }
        else if (framing.Kind == HttpRequestBodyFramingKind.Chunked || IsChunkedBesideContentLength(head, framing))
        {
            await ReadChunksAsync(reader, cancellationToken);
        }
    }

    // A test case may send both fields (test 60's -H "Content-Length: 1" beside -T -); the server half reads the chunks,
    // as RFC 9112 section 6.3 rule 3 has Transfer-Encoding override Content-Length. The generic framing refuses the pair on purpose (ADR-0024).
    private static bool IsChunkedBesideContentLength(HttpRequestHead head, HttpRequestBodyFraming framing)
    {
        var transferEncodings = head.GetFieldValues("Transfer-Encoding");

        return framing.Kind == HttpRequestBodyFramingKind.Unreadable
            && head.Version.Minor >= 1
            && transferEncodings.Count == 1
            && string.Equals(transferEncodings[0], "chunked", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUpload(HttpRequestHead head) => head.Method is "POST" or "PUT";

    private static bool IsLeftUnread(HttpRequestHead head, HttpUpstreamTestCaseServerCommands commands) =>
        (commands.AuthRequired && IsUpload(head) && head.GetFieldValues("Authorization").Count == 0)
        || (commands.NoExpect && head.GetFieldValues("Expect").Count > 0);

    // False when the client half-closed before the count was read.
    private static async Task<bool> ReadExactlyAsync(HttpConnectionReader reader, long count, CancellationToken cancellationToken)
    {
        var buffer = new byte[(int)Math.Min(count, 64 * 1024) + 1];
        var left = count;
        while (left > 0)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, (int)Math.Min(left, buffer.Length)), cancellationToken);
            if (read == 0)
            {
                return false;
            }

            left -= read;
        }

        return true;
    }

    // Each chunk: its size line, its data and CRLF; the last chunk's size is 0 and the trailer
    // ends at an empty line. A line that is too long, or a size that is not hexadecimal, ends the read.
    private static async Task ReadChunksAsync(HttpConnectionReader reader, CancellationToken cancellationToken)
    {
        var size = await ReadChunkSizeAsync(reader, cancellationToken);
        while (size > 0 && await ReadExactlyAsync(reader, size + 2, cancellationToken))
        {
            size = await ReadChunkSizeAsync(reader, cancellationToken);
        }

        if (size == 0)
        {
            await ReadTrailerAsync(reader, cancellationToken);
        }
    }

    // The size, or -1 when there is no size line to read.
    private static async Task<long> ReadChunkSizeAsync(HttpConnectionReader reader, CancellationToken cancellationToken)
    {
        var line = await reader.ReadLineAsync(MaxChunkLineBytes, cancellationToken);
        var text = line is null ? string.Empty : Encoding.Latin1.GetString(line).Split(';')[0].Trim();

        return long.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var size) && size >= 0 ? size : -1;
    }

    private static async Task ReadTrailerAsync(HttpConnectionReader reader, CancellationToken cancellationToken)
    {
        var line = await reader.ReadLineAsync(MaxChunkLineBytes, cancellationToken);
        while (line is not null && line.AsSpan().TrimEnd((byte)'\r').Length > 0)
        {
            line = await reader.ReadLineAsync(MaxChunkLineBytes, cancellationToken);
        }
    }
}
