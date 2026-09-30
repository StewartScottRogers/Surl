using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.LineProtocol;

/// <summary>
/// Writes a message as a dot-stuffed multi-line body (RFC 1939, section 3; ADR-0050, decision 8),
/// for POP3's <c>RETR</c> and <c>TOP</c>.
/// </summary>
public static class DotStuffedBodyWriter
{
    /// <summary>
    /// Writes <paramref name="message"/> to <paramref name="connection"/> in one write: a <c>.</c>
    /// before every line that starts with <c>.</c> (at the start and after each CRLF, not after
    /// a bare LF), a CRLF when a non-empty message does not end with one, then <c>.</c> CRLF.
    /// An empty message is written as <c>.</c> CRLF alone.
    /// </summary>
    /// <param name="connection">The connection to write to.</param>
    /// <param name="message">The message's bytes.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes when the bytes are written.</returns>
    public static ValueTask WriteAsync(IConnection connection, ReadOnlyMemory<byte> message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return connection.WriteAsync(Stuff(message.Span), cancellationToken);
    }

    /// <summary>
    /// The bytes <see cref="WriteAsync"/> writes for <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The message's bytes.</param>
    /// <returns>The dot-stuffed body with its terminator.</returns>
    internal static ReadOnlyMemory<byte> Stuff(ReadOnlySpan<byte> message)
    {
        var stuffed = new ArrayBufferWriter<byte>(message.Length + 8);
        if (message.StartsWith("."u8))
        {
            stuffed.Write("."u8);
        }

        var rest = message;
        for (var found = rest.IndexOf("\r\n."u8); found >= 0; found = rest.IndexOf("\r\n."u8))
        {
            stuffed.Write(rest[..(found + 2)]);
            stuffed.Write("."u8);
            rest = rest[(found + 2)..];
        }

        stuffed.Write(rest);
        if (!message.IsEmpty && !message.EndsWith("\r\n"u8))
        {
            stuffed.Write("\r\n"u8);
        }

        stuffed.Write(".\r\n"u8);

        return stuffed.WrittenMemory;
    }
}
