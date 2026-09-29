using System.Globalization;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Output;

/// <summary>
/// One exchange's <c>-v</c> log (ADR-0007, section 8): <c>#&lt;id&gt; &lt; </c> for bytes
/// received, <c>#&lt;id&gt; &gt; </c> for bytes sent and <c>#&lt;id&gt; * </c> for a note,
/// each followed by the escaped text and <see cref="Environment.NewLine"/>.
/// </summary>
internal sealed class VerboseExchangeLog : IExchangeLog
{
    /// <summary>
    /// The most bytes one log line holds when they contain no LF.
    /// </summary>
    internal const int MaximumBytesPerLine = 1024;

    private readonly string receivedPrefix;
    private readonly string sentPrefix;
    private readonly string notePrefix;
    private readonly TextWriter writer;
    private readonly Lock writeLock;

    /// <summary>
    /// Creates the log for one exchange.
    /// </summary>
    /// <param name="exchangeId">The exchange's id, written in decimal on every line.</param>
    /// <param name="writer">The writer every exchange's log shares.</param>
    /// <param name="writeLock">The lock every exchange's log shares, held while writing.</param>
    public VerboseExchangeLog(long exchangeId, TextWriter writer, Lock writeLock)
        : this(exchangeId.ToString(CultureInfo.InvariantCulture), writer, writeLock)
    {
    }

    private VerboseExchangeLog(string exchangeIdText, TextWriter writer, Lock writeLock)
    {
        var id = "#" + exchangeIdText;
        receivedPrefix = id + " < ";
        sentPrefix = id + " > ";
        notePrefix = id + " * ";
        this.writer = writer;
        this.writeLock = writeLock;
    }

    /// <summary>
    /// Creates the log for events that belong to no exchange: its lines carry <c>-</c> where
    /// an exchange id goes (ADR-0028).
    /// </summary>
    /// <param name="writer">The writer every exchange's log shares.</param>
    /// <param name="writeLock">The lock every exchange's log shares, held while writing.</param>
    /// <returns>The log.</returns>
    public static VerboseExchangeLog OutsideAnyExchange(TextWriter writer, Lock writeLock) =>
        new("-", writer, writeLock);

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

        var builder = new StringBuilder(notePrefix);
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

        var builder = new StringBuilder();
        while (!bytes.IsEmpty)
        {
            var length = LengthOfFirstLine(bytes);
            builder.Append(prefix);
            ExchangeLogEscaping.AppendEscaped(bytes[..length], builder);
            builder.Append(Environment.NewLine);
            bytes = bytes[length..];
        }

        Write(builder.ToString());
    }

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
