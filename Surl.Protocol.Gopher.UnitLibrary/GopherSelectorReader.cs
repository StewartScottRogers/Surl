using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Gopher;

/// <summary>
/// Reads the one request line a Gopher client sends: a selector, optionally a TAB and a
/// search string, and a line ending (RFC 1436, section 2).
/// </summary>
internal static class GopherSelectorReader
{
    private const int ReadBufferBytes = 1024;

    /// <summary>
    /// Reads up to the first line feed, and strips the line feed and a carriage return
    /// before it. Bytes after the line feed are never read: a Gopher client sends nothing more.
    /// </summary>
    /// <param name="connection">The connection to read from.</param>
    /// <param name="maxLineBytes">The most bytes the line may hold, line ending included; 0 is no limit.</param>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The outcome, and the line when it is <see cref="GopherSelectorReadOutcome.LineRead"/>.</returns>
    public static async Task<(GopherSelectorReadOutcome Outcome, byte[] Line)> ReadAsync(
        IConnection connection, long maxLineBytes, CancellationToken cancellationToken)
    {
        var line = new MemoryStream();
        var buffer = new byte[ReadBufferBytes];

        while (true)
        {
            var read = await connection.ReadAsync(buffer.AsMemory(0, BytesToRead(buffer.Length, maxLineBytes, line.Length)), cancellationToken);
            if (read == 0)
            {
                return (GopherSelectorReadOutcome.ConnectionClosed, []);
            }

            var lineFeed = Array.IndexOf(buffer, (byte)'\n', 0, read);
            line.Write(buffer, 0, lineFeed < 0 ? read : lineFeed + 1);
            if (IsPastLimit(line.Length, maxLineBytes))
            {
                return (GopherSelectorReadOutcome.LineTooLong, []);
            }

            if (lineFeed >= 0)
            {
                return (GopherSelectorReadOutcome.LineRead, WithoutLineEnding(line.ToArray()));
            }
        }
    }

    // Never asks for more than one byte past the limit, so a long line is not read further.
    private static int BytesToRead(int bufferLength, long maxLineBytes, long lineLength) =>
        maxLineBytes == 0 ? bufferLength : (int)Math.Min(bufferLength, maxLineBytes + 1 - lineLength);

    private static bool IsPastLimit(long lineLength, long maxLineBytes) =>
        maxLineBytes != 0 && lineLength > maxLineBytes;

    private static byte[] WithoutLineEnding(byte[] line)
    {
        var end = line.Length - 1;
        return end > 0 && line[end - 1] == (byte)'\r' ? line[..(end - 1)] : line[..end];
    }
}
