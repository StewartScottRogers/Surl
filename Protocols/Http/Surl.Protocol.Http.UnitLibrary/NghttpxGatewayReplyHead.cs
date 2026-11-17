using System.Text;

namespace Surl.Protocol.Http;

/// <summary>
/// Finds the end of a backend reply's head and splits it into lines, accepting a bare <c>LF</c> as a
/// line end as nghttpx's HTTP/1 parser does: a case's reply part without <c>crlf="headers"</c>
/// (test 2407) is sent verbatim with <c>LF</c>-only lines. Pure: no socket and no state.
/// </summary>
internal static class NghttpxGatewayReplyHead
{
    /// <summary>Finds the blank line that ends the head.</summary>
    /// <param name="reply">The bytes from the start of a reply head.</param>
    /// <param name="lines">The head's lines, without their line ends and without the blank line.</param>
    /// <param name="bodyStart">The index of the first byte after the blank line.</param>
    /// <returns>Whether the bytes hold a whole head.</returns>
    public static bool TryRead(ReadOnlySpan<byte> reply, out string[] lines, out int bodyStart)
    {
        int lineStart = 0;
        List<string> found = [];
        while (true)
        {
            int lineFeed = reply[lineStart..].IndexOf((byte)'\n');
            if (lineFeed < 0)
            {
                lines = [];
                bodyStart = 0;

                return false;
            }

            ReadOnlySpan<byte> line = reply.Slice(lineStart, lineFeed);
            if (line.EndsWith("\r"u8))
            {
                line = line[..^1];
            }

            lineStart += lineFeed + 1;
            if (line.IsEmpty && found.Count > 0)
            {
                lines = [.. found];
                bodyStart = lineStart;

                return true;
            }

            if (!line.IsEmpty)
            {
                found.Add(Encoding.Latin1.GetString(line));
            }
        }
    }
}
