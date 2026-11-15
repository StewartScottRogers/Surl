namespace Surl.Control;

/// <summary>One message with its <c>message/rfc822</c> bytes.</summary>
/// <param name="Summary">The message without its bytes.</param>
/// <param name="Bytes">The bytes as stored.</param>
public sealed record ControlMailMessage(ControlMailMessageSummary Summary, ReadOnlyMemory<byte> Bytes);
