namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// An Application Message the broker publishes (MQTT 5.0 section 1.2): a topic, a payload, the
/// QoS and RETAIN it is published with, and the properties that travel with it.
/// </summary>
/// <param name="Topic">The Topic Name.</param>
/// <param name="Payload">The payload.</param>
/// <param name="QualityOfService">The QoS it is published at, 0 to 2.</param>
/// <param name="Retain">Whether it is published as a retained message.</param>
/// <param name="Properties">The properties forwarded with it, in the order they arrived.</param>
internal sealed record Mqtt5ApplicationMessage(
    string Topic,
    byte[] Payload,
    int QualityOfService,
    bool Retain,
    IReadOnlyList<Mqtt5Property> Properties)
{
    /// <summary>
    /// The message a Will stands for (section 3.1.2.5): its topic, payload, Will QoS, Will Retain
    /// and Will Properties, less the Will Delay Interval, which governs only when it is published.
    /// </summary>
    /// <param name="will">The Will of a <c>CONNECT</c>.</param>
    /// <returns>The message.</returns>
    public static Mqtt5ApplicationMessage FromWill(Mqtt5Will will) => new(
        will.Topic,
        will.Payload,
        will.QualityOfService,
        will.Retain,
        [.. will.Properties.Where(property => property.Identifier != Mqtt5PropertyIdentifier.WillDelayInterval)]);
}
