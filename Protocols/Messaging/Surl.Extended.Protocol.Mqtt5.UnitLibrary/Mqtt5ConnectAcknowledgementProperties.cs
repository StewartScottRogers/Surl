namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// The properties of the <c>CONNACK</c> that accepts a <c>CONNECT</c> (ADR-0093 decision 1's
/// table), and the Session Expiry Interval and Keep Alive they put in force.
/// </summary>
internal static class Mqtt5ConnectAcknowledgementProperties
{
    /// <summary>
    /// The CONNACK Properties, in the ADR's order: Session Expiry Interval when the broker lowers
    /// the client's, Receive Maximum, Maximum Packet Size unless there is no packet limit, the
    /// Assigned Client Identifier when one was assigned, Topic Alias Maximum, and Server Keep
    /// Alive when the client's Keep Alive does not stand.
    /// </summary>
    /// <param name="connect">The accepted <c>CONNECT</c>.</param>
    /// <param name="assignedClientIdentifier">The identifier the broker assigned, or <see langword="null"/>.</param>
    /// <param name="maxPacketBytes">The packet limit, fixed header included; 0 for none.</param>
    /// <returns>The properties.</returns>
    public static IReadOnlyList<Mqtt5Property> For(Mqtt5ConnectPacket connect, string? assignedClientIdentifier, long maxPacketBytes)
    {
        var properties = new List<Mqtt5Property>();
        if (RequestedSessionExpirySeconds(connect) > Mqtt5Broker.MaxSessionExpirySeconds)
        {
            properties.Add(Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.SessionExpiryInterval, Mqtt5Broker.MaxSessionExpirySeconds));
        }

        properties.Add(Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.ReceiveMaximum, Mqtt5Broker.ReceiveMaximum));
        if (maxPacketBytes != 0)
        {
            properties.Add(Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.MaximumPacketSize, (uint)Math.Min(maxPacketBytes, uint.MaxValue)));
        }

        if (assignedClientIdentifier is not null)
        {
            properties.Add(Mqtt5Property.FromText(Mqtt5PropertyIdentifier.AssignedClientIdentifier, assignedClientIdentifier));
        }

        properties.Add(Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.TopicAliasMaximum, Mqtt5Broker.TopicAliasMaximum));
        if (!KeepAliveStands(connect.KeepAlive))
        {
            properties.Add(Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.ServerKeepAlive, Mqtt5Broker.MaxKeepAliveSeconds));
        }

        return properties;
    }

    /// <summary>
    /// The Keep Alive in force (<c>[MQTT-3.2.2-22]</c>): the client's when it stands, otherwise
    /// <see cref="Mqtt5Broker.MaxKeepAliveSeconds"/>.
    /// </summary>
    /// <param name="connect">The accepted <c>CONNECT</c>.</param>
    /// <returns>The Keep Alive.</returns>
    public static TimeSpan KeepAliveInForce(Mqtt5ConnectPacket connect) =>
        TimeSpan.FromSeconds(KeepAliveStands(connect.KeepAlive) ? connect.KeepAlive : Mqtt5Broker.MaxKeepAliveSeconds);

    /// <summary>
    /// The Session Expiry Interval in force: the client's (0 when absent), capped at
    /// <see cref="Mqtt5Broker.MaxSessionExpirySeconds"/>.
    /// </summary>
    /// <param name="connect">The accepted <c>CONNECT</c>.</param>
    /// <returns>The interval.</returns>
    public static TimeSpan SessionExpiryInForce(Mqtt5ConnectPacket connect) =>
        TimeSpan.FromSeconds(Math.Min(RequestedSessionExpirySeconds(connect), Mqtt5Broker.MaxSessionExpirySeconds));

    private static uint RequestedSessionExpirySeconds(Mqtt5ConnectPacket connect) =>
        Mqtt5ConnectJudge.Find(connect.Properties, Mqtt5PropertyIdentifier.SessionExpiryInterval)?.Integer ?? 0;

    private static bool KeepAliveStands(ushort keepAlive) => keepAlive is > 0 and <= Mqtt5Broker.MaxKeepAliveSeconds;
}
