namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// One STOMP frame (the specification's "STOMP Frames"): a command, its headers in the order sent,
/// each name once, and its body.
/// </summary>
/// <param name="Command">The command, as sent; commands match case-sensitively.</param>
/// <param name="Headers">The headers, decoded, the first of any repeated name kept (ADR-0118 decision 0).</param>
/// <param name="Body">The body, NULs included when <c>content-length</c> framed it.</param>
internal sealed record StompFrame(string Command, IReadOnlyList<KeyValuePair<string, string>> Headers, ReadOnlyMemory<byte> Body)
{
    /// <summary>Creates a frame without a body.</summary>
    /// <param name="command">The command.</param>
    /// <param name="headers">The headers, in order.</param>
    public StompFrame(string command, params KeyValuePair<string, string>[] headers)
        : this(command, headers, ReadOnlyMemory<byte>.Empty)
    {
    }

    /// <summary>The value of the header <paramref name="name"/>, or <see langword="null"/> when the frame has none.</summary>
    /// <param name="name">The header name, matched case-sensitively.</param>
    public string? Header(string name)
    {
        foreach (var header in Headers)
        {
            if (header.Key == name)
            {
                return header.Value;
            }
        }

        return null;
    }
}
