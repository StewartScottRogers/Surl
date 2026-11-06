namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// The Will Message a <c>CONNECT</c> carries when its Will Flag is set (sections 3.1.2.5 to
/// 3.1.2.7 and 3.1.3.2 to 3.1.3.4).
/// </summary>
/// <param name="QualityOfService">The Will QoS, 0 to 2.</param>
/// <param name="Retain">The Will Retain flag.</param>
/// <param name="Properties">The Will Properties.</param>
/// <param name="Topic">The Will Topic.</param>
/// <param name="Payload">The Will Payload.</param>
internal sealed record Mqtt5Will(
    int QualityOfService,
    bool Retain,
    IReadOnlyList<Mqtt5Property> Properties,
    string Topic,
    byte[] Payload);
