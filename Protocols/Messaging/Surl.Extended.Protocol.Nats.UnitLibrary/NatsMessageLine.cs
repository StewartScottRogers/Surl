using System.Text;

namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// One delivery as the protocol page writes it (ADR-0113 decision 4):
/// <c>MSG &lt;subject&gt; &lt;sid&gt; [reply-to] &lt;#bytes&gt;</c> CR LF, the payload, CR LF; or,
/// with a header block, <c>HMSG &lt;subject&gt; &lt;sid&gt; [reply-to] &lt;#header bytes&gt;
/// &lt;#total bytes&gt;</c> CR LF, the block and payload, CR LF.
/// </summary>
internal static class NatsMessageLine
{
    /// <summary>The bytes of one MSG, or of one HMSG when <paramref name="headerBlock"/> is given.</summary>
    public static byte[] Encode(string subject, string sid, string? replyTo, ReadOnlySpan<byte> headerBlock, bool withHeaders, ReadOnlySpan<byte> payload)
    {
        var line = new StringBuilder(withHeaders ? "HMSG " : "MSG ").Append(subject).Append(' ').Append(sid);
        if (replyTo is not null)
        {
            line.Append(' ').Append(replyTo);
        }

        if (withHeaders)
        {
            line.Append(' ').Append(headerBlock.Length);
        }

        line.Append(' ').Append(headerBlock.Length + payload.Length).Append("\r\n");
        var head = Encoding.UTF8.GetBytes(line.ToString());
        var bytes = new byte[head.Length + headerBlock.Length + payload.Length + 2];
        head.CopyTo(bytes, 0);
        headerBlock.CopyTo(bytes.AsSpan(head.Length));
        payload.CopyTo(bytes.AsSpan(head.Length + headerBlock.Length));
        bytes[^2] = (byte)'\r';
        bytes[^1] = (byte)'\n';
        return bytes;
    }
}
