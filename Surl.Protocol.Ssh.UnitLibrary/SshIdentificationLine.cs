using System.Text;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The protocol version exchange of RFC 4253 section 4.2, as ADR-0051 decision 1 decides it:
/// the line the server sends, and the reading of the client's.
/// </summary>
internal static class SshIdentificationLine
{
    /// <summary>
    /// The longest identification line accepted, its line ending included (RFC 4253, section 4.2).
    /// </summary>
    public const int MaxLineBytes = 255;

    private const string ProtocolVersionNotSupported = "Protocol version not supported";

    /// <summary>
    /// The line the server sends first, CR LF included: no version number and no comment
    /// (ADR-0006 section 3, ADR-0051 decision 1).
    /// </summary>
    public static ReadOnlyMemory<byte> ServerLine { get; } = Encoding.ASCII.GetBytes("SSH-2.0-surl\r\n");

    /// <summary>
    /// Reads the client's identification line: it must be the first line the client sends,
    /// start <c>SSH-2.0-</c> or <c>SSH-1.99-</c>, end at CR LF or a bare LF, hold at most
    /// <see cref="MaxLineBytes"/> bytes with its ending and hold no NUL.
    /// </summary>
    /// <param name="reader">The buffered reader over the connection.</param>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The line without its ending, as the key exchange hashes it.</returns>
    /// <exception cref="SshExchangeEndedException">The client closed the connection before the line ended.</exception>
    /// <exception cref="SshDisconnectRequiredException">The line is refused: <c>DISCONNECT</c> 8.</exception>
    public static async ValueTask<byte[]> ReadClientLineAsync(SshConnectionReader reader, CancellationToken cancellationToken)
    {
        var line = new List<byte>();
        while (line.Count < MaxLineBytes)
        {
            var next = await reader.ReadByteAsync(cancellationToken);
            if (next < 0)
            {
                throw new SshExchangeEndedException(
                    line.Count == 0 ? null : "The client closed the connection part way through its SSH identification line.");
            }

            if (next == '\n')
            {
                return AcceptedLine(line);
            }

            line.Add((byte)next);
        }

        throw Refused($"The client's SSH identification line was longer than {MaxLineBytes} bytes.");
    }

    private static byte[] AcceptedLine(List<byte> lineWithoutLineFeed)
    {
        var line = lineWithoutLineFeed.ToArray();
        if (line.Length > 0 && line[^1] == '\r')
        {
            line = line[..^1];
        }

        if (line.Contains((byte)0) || !(StartsWith(line, "SSH-2.0-") || StartsWith(line, "SSH-1.99-")))
        {
            throw Refused($"The client's SSH identification line is not an SSH 2.0 one: {SshLogText.Render(line)}");
        }

        return line;
    }

    private static bool StartsWith(byte[] line, string prefix) => line.AsSpan().StartsWith(Encoding.ASCII.GetBytes(prefix));

    private static SshDisconnectRequiredException Refused(string note) =>
        new(SshDisconnectReason.ProtocolVersionNotSupported, ProtocolVersionNotSupported, note);
}
