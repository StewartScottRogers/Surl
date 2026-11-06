using System.Text;

namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// Writes frames (ADR-0118 decision 0): the command, each header <c>name:value</c>, an empty line,
/// the body and a NUL, every line ended with a bare LF.
/// </summary>
internal static class StompFrameWriter
{
    /// <summary>The frame's bytes.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="escaping">
    /// The version whose escapes the header names and values get, or <see langword="null"/> for
    /// none: before a version is negotiated, and always for CONNECTED.
    /// </param>
    public static byte[] Encode(StompFrame frame, StompVersion? escaping)
    {
        var head = new StringBuilder();
        head.Append(frame.Command).Append('\n');
        foreach (var header in frame.Headers)
        {
            head.Append(Escaped(header.Key, escaping)).Append(':').Append(Escaped(header.Value, escaping)).Append('\n');
        }

        head.Append('\n');
        var headBytes = Encoding.UTF8.GetBytes(head.ToString());
        var bytes = new byte[headBytes.Length + frame.Body.Length + 1];
        headBytes.CopyTo(bytes, 0);
        frame.Body.Span.CopyTo(bytes.AsSpan(headBytes.Length));
        return bytes;
    }

    private static string Escaped(string text, StompVersion? escaping) =>
        escaping is { } version ? StompHeaderEscaping.Encode(text, version) : text;
}
