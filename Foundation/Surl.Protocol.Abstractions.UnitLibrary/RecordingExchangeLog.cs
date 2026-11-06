namespace Surl.Protocol.Abstractions;

/// <summary>
/// An <see cref="IExchangeLog"/> that records every call in order, so a protocol test can
/// build an <see cref="ExchangeContext"/> and assert what a server logged (ADR-0004, section 7).
/// </summary>
public sealed class RecordingExchangeLog : IExchangeLog
{
    private readonly List<ExchangeLogEntry> entries = [];
    private readonly Lock entriesLock = new();

    /// <summary>
    /// A snapshot of every call recorded so far, in order.
    /// </summary>
    public IReadOnlyList<ExchangeLogEntry> Entries
    {
        get
        {
            lock (entriesLock)
            {
                return [.. entries];
            }
        }
    }

    /// <summary>
    /// The text of every note recorded so far, in order.
    /// </summary>
    public IReadOnlyList<string> Notes =>
        Entries.Where(entry => entry.Kind == ExchangeLogEntryKind.Note).Select(entry => entry.Text).ToList();

    /// <inheritdoc/>
    public void BytesReceived(ReadOnlySpan<byte> bytes) =>
        Record(new ExchangeLogEntry(ExchangeLogEntryKind.BytesReceived, bytes.ToArray(), string.Empty));

    /// <inheritdoc/>
    public void BytesSent(ReadOnlySpan<byte> bytes) =>
        Record(new ExchangeLogEntry(ExchangeLogEntryKind.BytesSent, bytes.ToArray(), string.Empty));

    /// <inheritdoc/>
    public void Note(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        Record(new ExchangeLogEntry(ExchangeLogEntryKind.Note, [], text));
    }

    private void Record(ExchangeLogEntry entry)
    {
        lock (entriesLock)
        {
            entries.Add(entry);
        }
    }
}
