namespace Surl.Control;

/// <summary>A topic's retained message.</summary>
/// <param name="Topic">The topic.</param>
/// <param name="QualityOfService">The QoS, 0 to 2.</param>
/// <param name="Payload">The payload.</param>
public sealed record ControlRetainedMessage(string Topic, int QualityOfService, ReadOnlyMemory<byte> Payload);
