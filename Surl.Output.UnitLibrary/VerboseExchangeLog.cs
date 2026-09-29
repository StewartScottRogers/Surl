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
    {
        var id = "#" + exchangeId.ToString(CultureInfo.InvariantCulture);
        receivedPrefix = id + " < ";
        sentPrefix = id + " > ";
        notePrefix = id + " * ";
        this.writer = writer;
        this.writeLock = writeLock;
    }

    /// <inheritdoc/>
    public void BytesReceived(ReadOnlySpan<byte> bytes) => WriteBytes(receivedPrefix, bytes);

    /// <inheritdoc/>
    public void BytesSent(ReadOnlySpan<byte> bytes) => WriteBytes(sentPrefix, bytes);

    /// <inheritdoc/>
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
