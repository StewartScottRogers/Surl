using System.Globalization;
using System.Text;

namespace Surl.Protocol.Http;

/// <summary>
/// Says how complete a backend's reply is so the gateway knows when to stop reading: at the
/// announced <c>Content-Length</c> or the last chunk rather than only at the backend's close, since
/// the case's <c>http</c> half keeps the connection open unless <c>swsclose</c> says otherwise.
/// Pure: no socket and no state.
/// </summary>
public static class NghttpxGatewayReplyFramingReader
{
    private static readonly byte[] HeadEnd = "\r\n\r\n"u8.ToArray();

    private static readonly byte[] LastChunk = "0\r\n\r\n"u8.ToArray();

    /// <summary>Classifies the bytes read so far, skipping interim <c>1xx</c> replies.</summary>
    /// <param name="reply">Every byte the backend has sent.</param>
    /// <param name="requestWasHead">Whether the request was <c>HEAD</c>, whose reply has no body whatever its fields say.</param>
    /// <returns>The reply's framing state.</returns>
    public static NghttpxGatewayReplyFraming Read(ReadOnlySpan<byte> reply, bool requestWasHead)
    {
        int offset = 0;
        while (true)
        {
            int headLength = reply[offset..].IndexOf(HeadEnd);
            if (headLength < 0)
            {
                return NghttpxGatewayReplyFraming.NoHead;
            }

            string[] lines = Encoding.Latin1.GetString(reply.Slice(offset, headLength)).Split("\r\n");
            int bodyStart = offset + headLength + HeadEnd.Length;
            int status = ReadStatus(lines[0]);
            if (status is >= 100 and < 200)
            {
                offset = bodyStart;
                continue;
            }

            return requestWasHead || status is 204 or 304
                ? NghttpxGatewayReplyFraming.Whole
                : ReadBodyFraming(lines, reply[bodyStart..]);
        }
    }

    private static int ReadStatus(string statusLine)
    {
        string[] parts = statusLine.Split(' ', 3);
        return parts.Length > 1 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int status)
            ? status
            : 502;
    }

    private static NghttpxGatewayReplyFraming ReadBodyFraming(string[] lines, ReadOnlySpan<byte> body)
    {
        if (FieldValue(lines, "transfer-encoding")?.Contains("chunked", StringComparison.OrdinalIgnoreCase) == true)
        {
            return body.EndsWith(LastChunk) ? NghttpxGatewayReplyFraming.Whole : NghttpxGatewayReplyFraming.Short;
        }

        if (FieldValue(lines, "content-length") is not { } text
            || !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int length))
        {
            return NghttpxGatewayReplyFraming.ReadToClose;
        }

        return body.Length >= length ? NghttpxGatewayReplyFraming.Whole : NghttpxGatewayReplyFraming.Short;
    }

    private static string? FieldValue(string[] lines, string name)
    {
        foreach (string line in lines.Skip(1))
        {
            int colon = line.IndexOf(':');
            if (colon > 0 && line.AsSpan(0, colon).Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return line[(colon + 1)..].Trim(' ', '\t');
            }
        }

        return null;
    }
}
