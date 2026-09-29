using System.Globalization;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Output;

/// <summary>
/// One exchange's <c>-v</c> log (ADR-0007, section 8): <c>#&lt;id&gt; &lt; </c> for bytes
/// received, <c>#&lt;id&gt; &gt; </c> for bytes sent and <c>#&lt;id&gt; * </c> for a note,
/// each followed by the escaped text and <see cref="Environment.NewLine"/>, and, with
/// <c>--trace-time</c>, each preceded by the local time the line was written (ADR-0033,
/// section 5).
/// </summary>
internal sealed class VerboseExchangeLog : IExchangeLog
{
    /// <summary>
    /// The most bytes one log line holds when they contain no LF.
    /// </summary>
    internal const int MaximumBytesPerLine = 1024;

    /// <summary>
    /// The <c>--trace-time</c> stamp's format, as upstream curl 8.21.0 writes it (ADR-0033,
    /// section 5): 24-hour local time, six fraction digits and a space.
    /// </summary>
    internal const string TimestampFormat = "HH:mm:ss.ffffff ";

    private readonly string receivedPrefix;
    private readonly string sentPrefix;
    private readonly string notePrefix;
    private readonly TextWriter writer;
    private readonly Lock writeLock;
    private readonly TimeProvider? timestampClock;

    /// <summary>
    /// Creates the log for one exchange.
    /// </summary>
    /// <param name="exchangeId">The exchange's id, written in decimal on every line.</param>
    /// <param name="writer">The writer every exchange's log shares.</param>
    /// <param name="writeLock">The lock every exchange's log shares, held while writing.</param>
    /// <param name="timestampClock">The clock whose local time stamps every line, or <see langword="null"/> for no stamps.</param>
    public VerboseExchangeLog(long exchangeId, TextWriter writer, Lock writeLock, TimeProvider? timestampClock)
        : this(exchangeId.ToString(CultureInfo.InvariantCulture), writer, writeLock, timestampClock)
    {
    }

    private VerboseExchangeLog(string exchangeIdText, TextWriter writer, Lock writeLock, TimeProvider? timestampClock)
    {
        var id = "#" + exchangeIdText;
        receivedPrefix = id + " < ";
        sentPrefix = id + " > ";
        notePrefix = id + " * ";
        this.writer = writer;
        this.writeLock = writeLock;
        this.timestampClock = timestampClock;
    }

    /// <summary>
    /// Creates the log for events that belong to no exchange: its lines carry <c>-</c> where
    /// an exchange id goes (ADR-0028).
    /// </summary>
    /// <param name="writer">The writer every exchange's log shares.</param>
    /// <param name="writeLock">The lock every exchange's log shares, held while writing.</param>
    /// <param name="timestampClock">The clock whose local time stamps every line, or <see langword="null"/> for no stamps.</param>
    /// <returns>The log.</returns>
    public static VerboseExchangeLog OutsideAnyExchange(TextWriter writer, Lock writeLock, TimeProvider? timestampClock) =>
        new("-", writer, writeLock, timestampClock);

    /// <inheritdoc/>
    public void BytesReceived(ReadOnlySpan<byte> bytes) => WriteBytes(receivedPrefix, bytes);

    /// <inheritdoc/>
    public void BytesSent(ReadOnlySpan<byte> bytes) => WriteBytes(sentPrefix, bytes);

    /// <inheritdoc/>
    /// <remarks>
    /// A note carries no marker of which part of its text a peer chose (a path, a header
    /// value, a user name), so every note's text is rendered as its UTF-8 bytes through
    /// <see cref="ExchangeLogEscaping"/>, the same rule as bytes received and sent
    /// (ADR-0006, section 3). Escaping changes how local paths and exception messages are
    /// shown, not whether.
    /// </remarks>
    public void Note(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var builder = new StringBuilder(Timestamp());
        builder.Append(notePrefix);
        ExchangeLogEscaping.AppendEscaped(Encoding.UTF8.GetBytes(text), builder);
        builder.Append(Environment.NewLine);
        Write(builder.ToString());
    }

    private void WriteBytes(string prefix, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        var stampedPrefix = Timestamp() + prefix;
        var builder = new StringBuilder();
        while (!bytes.IsEmpty)
        {
            var length = LengthOfFirstLine(bytes);
            builder.Append(stampedPrefix);
            ExchangeLogEscaping.AppendEscaped(bytes[..length], builder);
            builder.Append(Environment.NewLine);
            bytes = bytes[length..];
        }

        Write(builder.ToString());
    }

    private string Timestamp() =>
        timestampClock?.GetLocalNow().ToString(TimestampFormat, CultureInfo.InvariantCulture) ?? string.Empty;

    private static int LengthOfFirstLine(ReadOnlySpan<byte> bytes)
    {
        var window = bytes[..Math.Min(bytes.Length, MaximumBytesPerLine)];
        var lineFeed = window.IndexOf((byte)'\n');
        return lineFeed < 0 ? window.Length : lineFeed + 1;
    }

    private void Write(string lines)
    {
        lock (writeLock)
        {
            writer.Write(lines);
        }
    }
}
