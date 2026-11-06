using System.Diagnostics.CodeAnalysis;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// One row of MQTT 5.0 table 2-4: a property's identifier, the type its value is written in,
/// the places it may appear, and the places it may appear more than once.
/// </summary>
/// <param name="Identifier">The property's identifier.</param>
/// <param name="Type">The data representation of its value.</param>
/// <param name="AllowedIn">Every place table 2-4 lets it appear.</param>
/// <param name="RepeatableIn">
/// The places it may appear more than once: User Property everywhere it is allowed
/// (section 3.1.2.11.8 and its peers), Subscription Identifier in <c>PUBLISH</c> only
/// (section 3.3.2.3.8; once in <c>SUBSCRIBE</c>, section 3.8.2.1.2). Every other property
/// appearing twice is a Protocol Error.
/// </param>
internal sealed record Mqtt5PropertyDefinition(
    Mqtt5PropertyIdentifier Identifier,
    Mqtt5PropertyType Type,
    Mqtt5PropertyContext AllowedIn,
    Mqtt5PropertyContext RepeatableIn)
{
    private const Mqtt5PropertyContext PublishAndWill = Mqtt5PropertyContext.Publish | Mqtt5PropertyContext.Will;

    private const Mqtt5PropertyContext ConnectAndAcknowledgement =
        Mqtt5PropertyContext.Connect | Mqtt5PropertyContext.ConnectAcknowledgement;

    private const Mqtt5PropertyContext Authenticating = ConnectAndAcknowledgement | Mqtt5PropertyContext.Authentication;

    private const Mqtt5PropertyContext ReasonStringPlaces =
        Mqtt5PropertyContext.ConnectAcknowledgement
        | Mqtt5PropertyContext.PublishAcknowledgement
        | Mqtt5PropertyContext.PublishReceived
        | Mqtt5PropertyContext.PublishRelease
        | Mqtt5PropertyContext.PublishComplete
        | Mqtt5PropertyContext.SubscribeAcknowledgement
        | Mqtt5PropertyContext.UnsubscribeAcknowledgement
        | Mqtt5PropertyContext.Disconnect
        | Mqtt5PropertyContext.Authentication;

    private const Mqtt5PropertyContext UserPropertyPlaces =
        ReasonStringPlaces
        | Mqtt5PropertyContext.Connect
        | PublishAndWill
        | Mqtt5PropertyContext.Subscribe
        | Mqtt5PropertyContext.Unsubscribe;

    private static readonly Dictionary<int, Mqtt5PropertyDefinition> DefinitionsByIdentifier = new Mqtt5PropertyDefinition[]
    {
        Once(Mqtt5PropertyIdentifier.PayloadFormatIndicator, Mqtt5PropertyType.Byte, PublishAndWill),
        Once(Mqtt5PropertyIdentifier.MessageExpiryInterval, Mqtt5PropertyType.FourByteInteger, PublishAndWill),
        Once(Mqtt5PropertyIdentifier.ContentType, Mqtt5PropertyType.Utf8String, PublishAndWill),
        Once(Mqtt5PropertyIdentifier.ResponseTopic, Mqtt5PropertyType.Utf8String, PublishAndWill),
        Once(Mqtt5PropertyIdentifier.CorrelationData, Mqtt5PropertyType.BinaryData, PublishAndWill),
        new(
            Mqtt5PropertyIdentifier.SubscriptionIdentifier,
            Mqtt5PropertyType.VariableByteInteger,
            Mqtt5PropertyContext.Publish | Mqtt5PropertyContext.Subscribe,
            Mqtt5PropertyContext.Publish),
        Once(
            Mqtt5PropertyIdentifier.SessionExpiryInterval,
            Mqtt5PropertyType.FourByteInteger,
            ConnectAndAcknowledgement | Mqtt5PropertyContext.Disconnect),
        Once(Mqtt5PropertyIdentifier.AssignedClientIdentifier, Mqtt5PropertyType.Utf8String, Mqtt5PropertyContext.ConnectAcknowledgement),
        Once(Mqtt5PropertyIdentifier.ServerKeepAlive, Mqtt5PropertyType.TwoByteInteger, Mqtt5PropertyContext.ConnectAcknowledgement),
        Once(Mqtt5PropertyIdentifier.AuthenticationMethod, Mqtt5PropertyType.Utf8String, Authenticating),
        Once(Mqtt5PropertyIdentifier.AuthenticationData, Mqtt5PropertyType.BinaryData, Authenticating),
        Once(Mqtt5PropertyIdentifier.RequestProblemInformation, Mqtt5PropertyType.Byte, Mqtt5PropertyContext.Connect),
        Once(Mqtt5PropertyIdentifier.WillDelayInterval, Mqtt5PropertyType.FourByteInteger, Mqtt5PropertyContext.Will),
        Once(Mqtt5PropertyIdentifier.RequestResponseInformation, Mqtt5PropertyType.Byte, Mqtt5PropertyContext.Connect),
        Once(Mqtt5PropertyIdentifier.ResponseInformation, Mqtt5PropertyType.Utf8String, Mqtt5PropertyContext.ConnectAcknowledgement),
        Once(
            Mqtt5PropertyIdentifier.ServerReference,
            Mqtt5PropertyType.Utf8String,
            Mqtt5PropertyContext.ConnectAcknowledgement | Mqtt5PropertyContext.Disconnect),
        Once(Mqtt5PropertyIdentifier.ReasonString, Mqtt5PropertyType.Utf8String, ReasonStringPlaces),
        Once(Mqtt5PropertyIdentifier.ReceiveMaximum, Mqtt5PropertyType.TwoByteInteger, ConnectAndAcknowledgement),
        Once(Mqtt5PropertyIdentifier.TopicAliasMaximum, Mqtt5PropertyType.TwoByteInteger, ConnectAndAcknowledgement),
        Once(Mqtt5PropertyIdentifier.TopicAlias, Mqtt5PropertyType.TwoByteInteger, Mqtt5PropertyContext.Publish),
        Once(Mqtt5PropertyIdentifier.MaximumQualityOfService, Mqtt5PropertyType.Byte, Mqtt5PropertyContext.ConnectAcknowledgement),
        Once(Mqtt5PropertyIdentifier.RetainAvailable, Mqtt5PropertyType.Byte, Mqtt5PropertyContext.ConnectAcknowledgement),
        new(Mqtt5PropertyIdentifier.UserProperty, Mqtt5PropertyType.Utf8StringPair, UserPropertyPlaces, UserPropertyPlaces),
        Once(Mqtt5PropertyIdentifier.MaximumPacketSize, Mqtt5PropertyType.FourByteInteger, ConnectAndAcknowledgement),
        Once(Mqtt5PropertyIdentifier.WildcardSubscriptionAvailable, Mqtt5PropertyType.Byte, Mqtt5PropertyContext.ConnectAcknowledgement),
        Once(Mqtt5PropertyIdentifier.SubscriptionIdentifierAvailable, Mqtt5PropertyType.Byte, Mqtt5PropertyContext.ConnectAcknowledgement),
        Once(Mqtt5PropertyIdentifier.SharedSubscriptionAvailable, Mqtt5PropertyType.Byte, Mqtt5PropertyContext.ConnectAcknowledgement),
    }.ToDictionary(definition => (int)definition.Identifier);

    /// <summary>
    /// Every row of table 2-4, in identifier order.
    /// </summary>
    public static IReadOnlyCollection<Mqtt5PropertyDefinition> All => DefinitionsByIdentifier.Values;

    /// <summary>
    /// Finds the row for an identifier read from the wire.
    /// </summary>
    /// <param name="identifier">The identifier, as read.</param>
    /// <param name="definition">The row; <see langword="null"/> when table 2-4 has none.</param>
    /// <returns>Whether table 2-4 defines <paramref name="identifier"/>.</returns>
    public static bool TryFind(int identifier, [NotNullWhen(true)] out Mqtt5PropertyDefinition? definition) =>
        DefinitionsByIdentifier.TryGetValue(identifier, out definition);

    /// <summary>
    /// The row for a defined identifier.
    /// </summary>
    /// <param name="identifier">A member of <see cref="Mqtt5PropertyIdentifier"/>.</param>
    /// <returns>Its row.</returns>
    /// <exception cref="KeyNotFoundException"><paramref name="identifier"/> is not a defined member.</exception>
    public static Mqtt5PropertyDefinition Of(Mqtt5PropertyIdentifier identifier) => DefinitionsByIdentifier[(int)identifier];

    /// <summary>
    /// Whether the property may appear in <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The place being read.</param>
    /// <returns><see langword="true"/> when table 2-4 allows it there.</returns>
    public bool IsAllowedIn(Mqtt5PropertyContext context) => (AllowedIn & context) != 0;

    /// <summary>
    /// Whether the property may appear more than once in <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The place being read.</param>
    /// <returns><see langword="true"/> when a second occurrence there is no Protocol Error.</returns>
    public bool IsRepeatableIn(Mqtt5PropertyContext context) => (RepeatableIn & context) != 0;

    private static Mqtt5PropertyDefinition Once(Mqtt5PropertyIdentifier identifier, Mqtt5PropertyType type, Mqtt5PropertyContext allowedIn) =>
        new(identifier, type, allowedIn, 0);
}
