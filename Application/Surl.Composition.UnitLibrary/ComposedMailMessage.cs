namespace Surl.Composition;

/// <summary>One stored message with its exact bytes, as a test reads it back.</summary>
/// <param name="Summary">The message's UID, size, flags and internal date.</param>
/// <param name="Bytes">A copy of the message as stored.</param>
public sealed record ComposedMailMessage(ComposedMailMessageSummary Summary, ReadOnlyMemory<byte> Bytes);
