namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// The MQTT 5.0 property identifiers (section 2.2.2.2, table 2-4).
/// </summary>
internal enum Mqtt5PropertyIdentifier
{
    /// <summary><c>0x01</c> Payload Format Indicator, a Byte.</summary>
    PayloadFormatIndicator = 0x01,

    /// <summary><c>0x02</c> Message Expiry Interval, a Four Byte Integer.</summary>
    MessageExpiryInterval = 0x02,

    /// <summary><c>0x03</c> Content Type, a UTF-8 Encoded String.</summary>
    ContentType = 0x03,

    /// <summary><c>0x08</c> Response Topic, a UTF-8 Encoded String.</summary>
    ResponseTopic = 0x08,

    /// <summary><c>0x09</c> Correlation Data, Binary Data.</summary>
    CorrelationData = 0x09,

    /// <summary><c>0x0B</c> Subscription Identifier, a Variable Byte Integer.</summary>
    SubscriptionIdentifier = 0x0B,

    /// <summary><c>0x11</c> Session Expiry Interval, a Four Byte Integer.</summary>
    SessionExpiryInterval = 0x11,

    /// <summary><c>0x12</c> Assigned Client Identifier, a UTF-8 Encoded String.</summary>
    AssignedClientIdentifier = 0x12,

    /// <summary><c>0x13</c> Server Keep Alive, a Two Byte Integer.</summary>
    ServerKeepAlive = 0x13,

    /// <summary><c>0x15</c> Authentication Method, a UTF-8 Encoded String.</summary>
    AuthenticationMethod = 0x15,

    /// <summary><c>0x16</c> Authentication Data, Binary Data.</summary>
    AuthenticationData = 0x16,

    /// <summary><c>0x17</c> Request Problem Information, a Byte.</summary>
    RequestProblemInformation = 0x17,

    /// <summary><c>0x18</c> Will Delay Interval, a Four Byte Integer.</summary>
    WillDelayInterval = 0x18,

    /// <summary><c>0x19</c> Request Response Information, a Byte.</summary>
    RequestResponseInformation = 0x19,

    /// <summary><c>0x1A</c> Response Information, a UTF-8 Encoded String.</summary>
    ResponseInformation = 0x1A,

    /// <summary><c>0x1C</c> Server Reference, a UTF-8 Encoded String.</summary>
    ServerReference = 0x1C,

    /// <summary><c>0x1F</c> Reason String, a UTF-8 Encoded String.</summary>
    ReasonString = 0x1F,

    /// <summary><c>0x21</c> Receive Maximum, a Two Byte Integer.</summary>
    ReceiveMaximum = 0x21,

    /// <summary><c>0x22</c> Topic Alias Maximum, a Two Byte Integer.</summary>
    TopicAliasMaximum = 0x22,

    /// <summary><c>0x23</c> Topic Alias, a Two Byte Integer.</summary>
    TopicAlias = 0x23,

    /// <summary><c>0x24</c> Maximum QoS, a Byte.</summary>
    MaximumQualityOfService = 0x24,

    /// <summary><c>0x25</c> Retain Available, a Byte.</summary>
    RetainAvailable = 0x25,

    /// <summary><c>0x26</c> User Property, a UTF-8 String Pair.</summary>
    UserProperty = 0x26,

    /// <summary><c>0x27</c> Maximum Packet Size, a Four Byte Integer.</summary>
    MaximumPacketSize = 0x27,

    /// <summary><c>0x28</c> Wildcard Subscription Available, a Byte.</summary>
    WildcardSubscriptionAvailable = 0x28,

    /// <summary><c>0x29</c> Subscription Identifier Available, a Byte.</summary>
    SubscriptionIdentifierAvailable = 0x29,

    /// <summary><c>0x2A</c> Shared Subscription Available, a Byte.</summary>
    SharedSubscriptionAvailable = 0x2A,
}
