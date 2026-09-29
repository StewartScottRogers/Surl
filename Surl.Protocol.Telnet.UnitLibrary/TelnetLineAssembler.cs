namespace Surl.Protocol.Telnet;

/// <summary>
/// Collects the client's data bytes into lines. A line ends at LF, with a CR directly before
/// it dropped, or at CR NUL, RFC 854's bare carriage return, which some TELNET clients send
/// for Enter. Upstream curl sends the bytes of its standard input unchanged.
/// </summary>
/// <remarks>
/// A line may hold at most the line limit in bytes, its line ending included (ADR-0006,
/// section 1); a byte that makes it longer is reported as <see cref="TelnetLineStatus.TooLong"/>
/// and the line is not kept. Not safe for concurrent calls.
/// </remarks>
internal sealed class TelnetLineAssembler
{
    private const byte CarriageReturn = (byte)'\r';
    private const byte LineFeed = (byte)'\n';
    private const byte Null = 0;

    private readonly long maxLineBytes;
    private readonly List<byte> line = [];

    /// <summary>
    /// Creates an assembler.
    /// </summary>
    /// <param name="maxLineBytes">The most bytes a line may hold, its line ending included; 0 means no limit.</param>
    public TelnetLineAssembler(long maxLineBytes)
    {
        this.maxLineBytes = maxLineBytes;
    }

    /// <summary>
    /// The line <see cref="Take"/> last completed, without its line ending.
    /// </summary>
    public byte[] CompletedLine { get; private set; } = [];

    /// <summary>
    /// Takes the next data byte.
    /// </summary>
    /// <param name="value">The byte.</param>
    /// <returns>Whether the byte completed a line, into <see cref="CompletedLine"/>, or made it too long.</returns>
    public TelnetLineStatus Take(byte value)
    {
        var endsLine = value == LineFeed || (value == Null && line.LastOrDefault(LineFeed) == CarriageReturn);
        if (endsLine)
        {
            return CompleteLine();
        }

        line.Add(value);

        return maxLineBytes > 0 && line.Count >= maxLineBytes ? TelnetLineStatus.TooLong : TelnetLineStatus.PartWay;
    }

    private TelnetLineStatus CompleteLine()
    {
        var length = line.LastOrDefault(LineFeed) == CarriageReturn ? line.Count - 1 : line.Count;
        CompletedLine = line.GetRange(0, length).ToArray();
        line.Clear();

        return TelnetLineStatus.Completed;
    }
}
