namespace Surl.Control;

/// <summary>A message <c>POST /v1/messages</c> seeds on the message bus: to a topic or to a queue, never both.</summary>
/// <param name="Topic">The topic it is published on, or <see langword="null"/> when it is enqueued.</param>
/// <param name="Queue">The queue it is enqueued on, or <see langword="null"/> when it is published.</param>
/// <param name="Payload">The message's bytes.</param>
/// <param name="Retain">Whether a published message is retained on its topic; never set for a queue.</param>
public sealed record ControlMessage(string? Topic, string? Queue, ReadOnlyMemory<byte> Payload, bool Retain);
