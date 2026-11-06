using System.Globalization;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Output;

/// <summary>
/// One exchange's trace dump (ADR-0033, section 4): each call with bytes is one event, a
/// <c>#&lt;id&gt; &lt;= Recv data, &lt;n&gt; bytes (0x&lt;n&gt;)</c> or
/// <c>#&lt;id&gt; =&gt; Send data, ...</c> header line and its <see cref="TraceDumpRows"/>,
/// written whole under the shared lock; a note is the verbose log's <c>#&lt;id&gt; * </c>
/// line. With <c>--trace-time</c>, header and note lines start with the local time they
/// were written; rows are never stamped (section 5).
/// </summary>
internal sealed class TraceExchangeLog : IExchangeLog
{
    private readonly string receivedHeaderPrefix;
    private readonly string sentHeaderPrefix;
    private readonly TraceDumpLayout layout;
    private readonly TextWriter writer;
    private readonly Lock writeLock;
    private readonly TimeProvider? timestampClock;
    private readonly VerboseExchangeLog noteWriter;

    /// <summary>
    /// Creates the dump for one exchange.
    /// </summary>
    /// <param name="exchangeId">The exchange's id, written in decimal on every header and note line.</param>
    /// <param name="layout">How each event's bytes are laid out.</param>
    /// <param name="writer">The writer every exchange's dump shares.</param>
    /// <param name="writeLock">The lock every exchange's dump shares, held while writing.</param>
    /// <param name="timestampClock">The clock whose local time stamps header and note lines, or <see langword="null"/> for no stamps.</param>
    public TraceExchangeLog(long exchangeId, TraceDumpLayout layout, TextWriter writer, Lock writeLock, TimeProvider? timestampClock)
    {
        var id = "#" + exchangeId.ToString(CultureInfo.InvariantCulture);
        receivedHeaderPrefix = id + " <= Recv data, ";
        sentHeaderPrefix = id + " => Send data, ";
        this.layout = layout;
        this.writer = writer;
        this.writeLock = writeLock;
        this.timestampClock = timestampClock;
        noteWriter = new VerboseExchangeLog(exchangeId, writer, writeLock, timestampClock);
    }

    /// <inheritdoc/>
    public void BytesReceived(ReadOnlySpan<byte> bytes) => WriteEvent(receivedHeaderPrefix, bytes);

    /// <inheritdoc/>
    public void BytesSent(ReadOnlySpan<byte> bytes) => WriteEvent(sentHeaderPrefix, bytes);

    /// <inheritdoc/>
    public void Note(string text) => noteWriter.Note(text);

    private void WriteEvent(string headerPrefix, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        var builder = new StringBuilder(VerboseExchangeLog.Timestamp(timestampClock));
        builder.Append(headerPrefix)
            .Append(bytes.Length.ToString(CultureInfo.InvariantCulture))
            .Append(" bytes (0x")
            .Append(bytes.Length.ToString("x", CultureInfo.InvariantCulture))
            .Append(')')
            .Append(Environment.NewLine);
        TraceDumpRows.Append(bytes, layout, builder);

        lock (writeLock)
        {
            writer.Write(builder.ToString());
        }
    }
}
