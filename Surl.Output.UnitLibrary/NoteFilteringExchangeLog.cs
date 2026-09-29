using Surl.Protocol.Abstractions;

namespace Surl.Output;

/// <summary>
/// One exchange's log at the <see cref="LogLevel.Error"/> and <see cref="LogLevel.Info"/>
/// levels (ADR-0033, section 3): writes no bytes, and writes a note, as the verbose log
/// writes it, only when the note's text starts with one of the given prefixes.
/// </summary>
internal sealed class NoteFilteringExchangeLog : IExchangeLog
{
    private readonly VerboseExchangeLog lineWriter;
    private readonly string[] writtenNotePrefixes;

    /// <summary>
    /// Creates the log for one exchange.
    /// </summary>
    /// <param name="lineWriter">The verbose log that writes the notes this log lets through.</param>
    /// <param name="writtenNotePrefixes">The starts of the notes to write, matched ordinally.</param>
    public NoteFilteringExchangeLog(VerboseExchangeLog lineWriter, string[] writtenNotePrefixes)
    {
        this.lineWriter = lineWriter;
        this.writtenNotePrefixes = writtenNotePrefixes;
    }

    /// <inheritdoc/>
    public void BytesReceived(ReadOnlySpan<byte> bytes)
    {
    }

    /// <inheritdoc/>
    public void BytesSent(ReadOnlySpan<byte> bytes)
    {
    }

    /// <inheritdoc/>
    public void Note(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (writtenNotePrefixes.Any(prefix => text.StartsWith(prefix, StringComparison.Ordinal)))
        {
            lineWriter.Note(text);
        }
    }
}
