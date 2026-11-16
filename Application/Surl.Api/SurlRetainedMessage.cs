namespace Surl.Api;

/// <summary>A topic's retained message, as <see cref="SurlServerMessages.ReadRetainedAsync"/> reads it.</summary>
/// <param name="Topic">The topic.</param>
/// <param name="QualityOfService">The message's QoS: 0, 1 or 2.</param>
/// <param name="Payload">Its payload bytes.</param>
public sealed record SurlRetainedMessage(string Topic, int QualityOfService, ReadOnlyMemory<byte> Payload);
