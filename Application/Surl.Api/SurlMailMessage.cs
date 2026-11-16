namespace Surl.Api;

/// <summary>One mail message with its exact bytes, as <see cref="SurlServerMail.ReadMessageAsync"/> reads it.</summary>
/// <param name="Summary">The message's metadata.</param>
/// <param name="Bytes">The message as stored (<c>message/rfc822</c>).</param>
public sealed record SurlMailMessage(SurlMessageSummary Summary, ReadOnlyMemory<byte> Bytes);
