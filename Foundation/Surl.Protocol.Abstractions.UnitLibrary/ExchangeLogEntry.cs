namespace Surl.Protocol.Abstractions;

/// <summary>
/// One call <see cref="RecordingExchangeLog"/> recorded.
/// </summary>
/// <param name="Kind">Which member was called.</param>
/// <param name="Bytes">A copy of the bytes passed; empty for a note.</param>
/// <param name="Text">The note's text; empty for bytes.</param>
public sealed record ExchangeLogEntry(ExchangeLogEntryKind Kind, byte[] Bytes, string Text);
