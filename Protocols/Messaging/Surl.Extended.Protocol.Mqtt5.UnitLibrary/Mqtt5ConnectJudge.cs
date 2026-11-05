using System.Collections.Frozen;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// The checks a decoded <c>CONNECT</c> still has to pass before its login is judged (ADR-0093
/// decision 1): the values of its properties and its Will's, and Authentication Data that comes
/// without a method. <see cref="Mqtt5ConnectPacket.Read"/> has already checked its layout, the
/// protocol name and level, the Connect Flags and which properties appear.
/// </summary>
internal static class Mqtt5ConnectJudge
{
    /// <summary>
    /// The property values MQTT 5.0 makes a Protocol Error (<c>0x82</c>), by property: a Receive
    /// Maximum or Maximum Packet Size of 0 (3.1.2.11.3, 3.1.2.11.4), a Request Response or Request
    /// Problem Information above 1 (3.1.2.11.6, 3.1.2.11.7), and a Will's Payload Format Indicator
    /// above 1 (3.1.3.2.3).
    /// </summary>
    private static readonly FrozenDictionary<Mqtt5PropertyIdentifier, (Func<uint, bool> IsAllowed, string Section)> ValueRules =
        new Dictionary<Mqtt5PropertyIdentifier, (Func<uint, bool> IsAllowed, string Section)>
        {
            [Mqtt5PropertyIdentifier.ReceiveMaximum] = (value => value != 0, "3.1.2.11.3"),
            [Mqtt5PropertyIdentifier.MaximumPacketSize] = (value => value != 0, "3.1.2.11.4"),
            [Mqtt5PropertyIdentifier.RequestResponseInformation] = (value => value <= 1, "3.1.2.11.6"),
            [Mqtt5PropertyIdentifier.RequestProblemInformation] = (value => value <= 1, "3.1.2.11.7"),
            [Mqtt5PropertyIdentifier.PayloadFormatIndicator] = (value => value <= 1, "3.1.3.2.3"),
        }.ToFrozenDictionary();

    /// <summary>
    /// Why <paramref name="connect"/> is refused, or <see langword="null"/> when its login is
    /// next: Authentication Data without an Authentication Method is a Protocol Error
    /// (3.1.2.11.10), and so is a property value MQTT 5.0 forbids. Whether a method is served is
    /// <see cref="Mqtt5EnhancedAuthentication.IsOffered"/>'s to say.
    /// </summary>
    /// <param name="connect">The <c>CONNECT</c>.</param>
    /// <returns>The fault to answer with, or <see langword="null"/>.</returns>
    public static Mqtt5PacketFault? FaultIn(Mqtt5ConnectPacket connect)
    {
        if (Find(connect.Properties, Mqtt5PropertyIdentifier.AuthenticationData) is not null
            && Find(connect.Properties, Mqtt5PropertyIdentifier.AuthenticationMethod) is null)
        {
            return Mqtt5PacketFault.ProtocolError("3.1.2.11.10");
        }

        return FaultInValues(connect.Properties) ?? FaultInValues(connect.Will?.Properties ?? []);
    }

    /// <summary>
    /// The first property of <paramref name="properties"/> with <paramref name="identifier"/>.
    /// </summary>
    /// <param name="properties">A property list.</param>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The property, or <see langword="null"/>.</returns>
    public static Mqtt5Property? Find(IReadOnlyList<Mqtt5Property> properties, Mqtt5PropertyIdentifier identifier) =>
        properties.FirstOrDefault(property => property.Identifier == identifier);

    private static Mqtt5PacketFault? FaultInValues(IReadOnlyList<Mqtt5Property> properties)
    {
        foreach (var property in properties)
        {
            if (ValueRules.TryGetValue(property.Identifier, out var rule) && !rule.IsAllowed(property.Integer))
            {
                return Mqtt5PacketFault.ProtocolError(rule.Section);
            }
        }

        return null;
    }
}
