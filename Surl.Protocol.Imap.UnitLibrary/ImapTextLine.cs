namespace Surl.Protocol.Imap;

/// <summary>
/// One line of a message's bytes: where it starts, where its content ends before the CRLF (or
/// lone LF), and where the next line starts.
/// </summary>
/// <param name="Start">The offset of its first byte.</param>
/// <param name="End">The offset just past its content.</param>
/// <param name="Next">The offset of the next line, or the length at the last one.</param>
internal readonly record struct ImapTextLine(int Start, int End, int Next)
{
    /// <summary>
    /// Whether it holds nothing before its line end.
    /// </summary>
    public bool IsEmpty => End == Start;

    /// <summary>
    /// The lines of <paramref name="text"/>, each ending at LF; a CR before the LF, or at the very
    /// end, is not content. A last line without LF counts.
    /// </summary>
    /// <param name="text">A message or a part of one.</param>
    /// <returns>The lines, in order.</returns>
    public static IEnumerable<ImapTextLine> Split(ReadOnlyMemory<byte> text)
    {
        var start = 0;
        while (start < text.Length)
        {
            var line = At(text.Span, start);
            yield return line;
            start = line.Next;
        }
    }

    private static ImapTextLine At(ReadOnlySpan<byte> text, int start)
    {
        var feed = text[start..].IndexOf((byte)'\n');
        var next = feed < 0 ? text.Length : start + feed + 1;
        var end = feed < 0 ? text.Length : start + feed;
        return new ImapTextLine(start, end > start && text[end - 1] == '\r' ? end - 1 : end, next);
    }
}
