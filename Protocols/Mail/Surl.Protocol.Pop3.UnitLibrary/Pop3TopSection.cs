namespace Surl.Protocol.Pop3;

/// <summary>
/// What <c>TOP</c> sends of a message (RFC 1939, section 7; ADR-0056, decision 5): the header
/// section, the empty line that ends it and the first lines of the body.
/// </summary>
internal static class Pop3TopSection
{
    /// <summary>
    /// The leading part of <paramref name="message"/> <c>TOP</c> sends.
    /// </summary>
    /// <param name="message">The message's bytes.</param>
    /// <param name="bodyLines">How many body lines to send, counted by CRLF.</param>
    /// <returns>The headers through the first empty line, then <paramref name="bodyLines"/> body
    /// lines; the whole message when it has no empty line or its body has no more lines.</returns>
    public static ReadOnlyMemory<byte> Slice(ReadOnlyMemory<byte> message, int bodyLines)
    {
        var end = HeaderSectionEnd(message.Span);
        for (var line = 0; line < bodyLines && end < message.Length; line++)
        {
            var lineEnd = message.Span[end..].IndexOf("\r\n"u8);
            end = lineEnd < 0 ? message.Length : end + lineEnd + 2;
        }

        return message[..end];
    }

    // The offset just past the empty line that ends the header section: CRLF CRLF, or the first
    // line itself when it is empty. A message without one is all header.
    private static int HeaderSectionEnd(ReadOnlySpan<byte> message)
    {
        if (message.StartsWith("\r\n"u8))
        {
            return 2;
        }

        var found = message.IndexOf("\r\n\r\n"u8);
        return found < 0 ? message.Length : found + 4;
    }
}
