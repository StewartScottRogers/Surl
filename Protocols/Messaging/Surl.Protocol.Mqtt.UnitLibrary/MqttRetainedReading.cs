namespace Surl.Protocol.Mqtt;

/// <summary>
/// What the bus answered one <c>SUBSCRIBE</c>, as <see cref="MqttBusRetainedMessages.Read"/> or
/// <see cref="MqttHeldSubscription.Subscribe"/> found it.
/// </summary>
/// <param name="Granted">Whether each filter was granted, in the order the filters were given.</param>
/// <param name="Matching">Every retained message a granted filter matches: each canonical topic once,
/// with its payload, in ordinal order of topic.</param>
internal sealed record MqttRetainedReading(
    IReadOnlyList<bool> Granted,
    IReadOnlyList<KeyValuePair<string, byte[]>> Matching);
