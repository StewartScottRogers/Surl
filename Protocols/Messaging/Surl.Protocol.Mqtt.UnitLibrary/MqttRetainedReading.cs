namespace Surl.Protocol.Mqtt;

/// <summary>
/// What <see cref="MqttBusRetainedMessages.Read"/> found for one <c>SUBSCRIBE</c>.
/// </summary>
/// <param name="Granted">Whether each filter was granted, in the order the filters were given.</param>
/// <param name="Matching">Every retained message a granted filter matches: each topic once, with its
/// payload, in ordinal order of topic.</param>
internal sealed record MqttRetainedReading(
    IReadOnlyList<bool> Granted,
    IReadOnlyList<KeyValuePair<string, byte[]>> Matching);
