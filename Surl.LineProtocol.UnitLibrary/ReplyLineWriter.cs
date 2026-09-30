using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.LineProtocol;

/// <summary>
/// Writes one reply line of printable ASCII followed by CRLF (ADR-0050, decision 8), so no
/// peer-supplied byte can inject a reply line.
/// </summary>
public static class ReplyLineWriter
{
    /// <summary>
    /// Writes <paramref name="text"/> and CRLF to <paramref name="connection"/> in one write.
    /// </summary>
    /// <param name="connection">The connection to write to.</param>
    /// <param name="text">The reply line without its CRLF; every character 0x20 to 0x7E.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes when the line is written.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> holds a CR, an LF or a character outside 0x20 to 0x7E.</exception>
    public static ValueTask WriteAsync(IConnection connection, string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(text);

        if (text.AsSpan().ContainsAnyExceptInRange((char)0x20, (char)0x7E))
        {
            throw new ArgumentException("A reply line holds only printable ASCII, 0x20 to 0x7E.", nameof(text));
        }

        var line = new byte[text.Length + 2];
        Encoding.ASCII.GetBytes(text, line);
        "\r\n"u8.CopyTo(line.AsSpan(text.Length));

        return connection.WriteAsync(line, cancellationToken);
    }
}
