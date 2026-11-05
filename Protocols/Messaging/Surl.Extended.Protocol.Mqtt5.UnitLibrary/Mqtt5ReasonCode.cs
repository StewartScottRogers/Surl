namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// The MQTT 5.0 reason codes (section 2.4, table 2-6). A value below <c>0x80</c> reports
/// success; one at or above it reports a failure.
/// </summary>
internal enum Mqtt5ReasonCode : byte
{
    /// <summary><c>0x00</c> Success; also Normal disconnection and Granted QoS 0.</summary>
    Success = 0x00,

    /// <summary><c>0x01</c> Granted QoS 1.</summary>
    GrantedQualityOfService1 = 0x01,

    /// <summary><c>0x02</c> Granted QoS 2.</summary>
    GrantedQualityOfService2 = 0x02,

    /// <summary><c>0x04</c> Disconnect with Will Message.</summary>
    DisconnectWithWillMessage = 0x04,

    /// <summary><c>0x10</c> No matching subscribers.</summary>
    NoMatchingSubscribers = 0x10,

    /// <summary><c>0x11</c> No subscription existed.</summary>
    NoSubscriptionExisted = 0x11,

    /// <summary><c>0x18</c> Continue authentication.</summary>
    ContinueAuthentication = 0x18,

    /// <summary><c>0x19</c> Re-authenticate.</summary>
    Reauthenticate = 0x19,

    /// <summary><c>0x80</c> Unspecified error.</summary>
    UnspecifiedError = 0x80,

    /// <summary><c>0x81</c> Malformed Packet: data within the packet could not be parsed.</summary>
    MalformedPacket = 0x81,

    /// <summary><c>0x82</c> Protocol Error: the packet parsed, but breaks the protocol.</summary>
    ProtocolError = 0x82,

    /// <summary><c>0x83</c> Implementation specific error.</summary>
    ImplementationSpecificError = 0x83,

    /// <summary><c>0x84</c> Unsupported Protocol Version.</summary>
    UnsupportedProtocolVersion = 0x84,

    /// <summary><c>0x85</c> Client Identifier not valid.</summary>
    ClientIdentifierNotValid = 0x85,

    /// <summary><c>0x86</c> Bad User Name or Password.</summary>
    BadUserNameOrPassword = 0x86,

    /// <summary><c>0x87</c> Not authorized.</summary>
    NotAuthorized = 0x87,

    /// <summary><c>0x88</c> Server unavailable.</summary>
    ServerUnavailable = 0x88,

    /// <summary><c>0x89</c> Server busy.</summary>
    ServerBusy = 0x89,

    /// <summary><c>0x8A</c> Banned.</summary>
    Banned = 0x8A,

    /// <summary><c>0x8B</c> Server shutting down.</summary>
    ServerShuttingDown = 0x8B,

    /// <summary><c>0x8C</c> Bad authentication method.</summary>
    BadAuthenticationMethod = 0x8C,

    /// <summary><c>0x8D</c> Keep Alive timeout.</summary>
    KeepAliveTimeout = 0x8D,

    /// <summary><c>0x8E</c> Session taken over.</summary>
    SessionTakenOver = 0x8E,

    /// <summary><c>0x8F</c> Topic Filter invalid.</summary>
    TopicFilterInvalid = 0x8F,

    /// <summary><c>0x90</c> Topic Name invalid.</summary>
    TopicNameInvalid = 0x90,

    /// <summary><c>0x91</c> Packet Identifier in use.</summary>
    PacketIdentifierInUse = 0x91,

    /// <summary><c>0x92</c> Packet Identifier not found.</summary>
    PacketIdentifierNotFound = 0x92,

    /// <summary><c>0x93</c> Receive Maximum exceeded.</summary>
    ReceiveMaximumExceeded = 0x93,

    /// <summary><c>0x94</c> Topic Alias invalid.</summary>
    TopicAliasInvalid = 0x94,

    /// <summary><c>0x95</c> Packet too large.</summary>
    PacketTooLarge = 0x95,

    /// <summary><c>0x96</c> Message rate too high.</summary>
    MessageRateTooHigh = 0x96,

    /// <summary><c>0x97</c> Quota exceeded.</summary>
    QuotaExceeded = 0x97,

    /// <summary><c>0x98</c> Administrative action.</summary>
    AdministrativeAction = 0x98,

    /// <summary><c>0x99</c> Payload format invalid.</summary>
    PayloadFormatInvalid = 0x99,

    /// <summary><c>0x9A</c> Retain not supported.</summary>
    RetainNotSupported = 0x9A,

    /// <summary><c>0x9B</c> QoS not supported.</summary>
    QualityOfServiceNotSupported = 0x9B,

    /// <summary><c>0x9C</c> Use another server.</summary>
    UseAnotherServer = 0x9C,

    /// <summary><c>0x9D</c> Server moved.</summary>
    ServerMoved = 0x9D,

    /// <summary><c>0x9E</c> Shared Subscriptions not supported.</summary>
    SharedSubscriptionsNotSupported = 0x9E,

    /// <summary><c>0x9F</c> Connection rate exceeded.</summary>
    ConnectionRateExceeded = 0x9F,

    /// <summary><c>0xA0</c> Maximum connect time.</summary>
    MaximumConnectTime = 0xA0,

    /// <summary><c>0xA1</c> Subscription Identifiers not supported.</summary>
    SubscriptionIdentifiersNotSupported = 0xA1,

    /// <summary><c>0xA2</c> Wildcard Subscriptions not supported.</summary>
    WildcardSubscriptionsNotSupported = 0xA2,
}
