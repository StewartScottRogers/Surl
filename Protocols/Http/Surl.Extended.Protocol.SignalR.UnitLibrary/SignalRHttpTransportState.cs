namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The parts of a Server-Sent Events or Long Polling connection that later requests reach through the
/// hub's connection store (ADR-0119, decision 2, amendment 1).
/// </summary>
/// <param name="Posts">Where HTTP Post bodies are handed to the session.</param>
/// <param name="LongPolling">The Long Polling transport, or <see langword="null"/> for Server-Sent Events.</param>
internal sealed record SignalRHttpTransportState(SignalRPostedPayloads Posts, SignalRLongPollingTransport? LongPolling);
