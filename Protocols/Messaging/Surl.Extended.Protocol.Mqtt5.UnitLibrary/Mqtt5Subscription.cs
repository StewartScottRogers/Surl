namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// One Topic Filter of a <c>SUBSCRIBE</c> and its Subscription Options (section 3.8.3.1).
/// </summary>
/// <param name="TopicFilter">The Topic Filter.</param>
/// <param name="MaximumQualityOfService">The Maximum QoS, 0 to 2.</param>
/// <param name="NoLocal">The No Local option.</param>
/// <param name="RetainAsPublished">The Retain As Published option.</param>
/// <param name="RetainHandling">The Retain Handling option, 0 to 2.</param>
internal sealed record Mqtt5Subscription(
    string TopicFilter,
    int MaximumQualityOfService,
    bool NoLocal,
    bool RetainAsPublished,
    int RetainHandling)
{
    private const int NoLocalFlag = 0x04;
    private const int RetainAsPublishedFlag = 0x08;
    private const int RetainHandlingShift = 4;
    private const int ReservedBits = 0xC0;

    /// <summary>
    /// The Subscription Options byte.
    /// </summary>
    public byte Options =>
        (byte)(MaximumQualityOfService
            | (NoLocal ? NoLocalFlag : 0)
            | (RetainAsPublished ? RetainAsPublishedFlag : 0)
            | (RetainHandling << RetainHandlingShift));

    /// <summary>
    /// Reads one Topic Filter and its options. Reserved bits set are a Malformed Packet
    /// (<c>[MQTT-3.8.3-5]</c>); a Maximum QoS or Retain Handling of 3 is a Protocol Error
    /// (section 3.8.3.1).
    /// </summary>
    /// <param name="reader">The <c>SUBSCRIBE</c> payload.</param>
    /// <returns>The subscription.</returns>
    /// <exception cref="Mqtt5PacketFaultException">The bytes break MQTT 5.0.</exception>
    public static Mqtt5Subscription Read(Mqtt5BodyReader reader)
    {
        var topicFilter = reader.ReadUtf8String();
        var options = reader.ReadByte();
        if ((options & ReservedBits) != 0)
        {
            throw Mqtt5BodyReader.Fault(Mqtt5PacketFault.Malformed("[MQTT-3.8.3-5]"));
        }

        var maximumQualityOfService = options & 0x03;
        var retainHandling = (options >> RetainHandlingShift) & 0x03;
        if (maximumQualityOfService == 3 || retainHandling == 3)
        {
            throw Mqtt5BodyReader.Fault(Mqtt5PacketFault.ProtocolError("3.8.3.1"));
        }

        return new(topicFilter, maximumQualityOfService, (options & NoLocalFlag) != 0, (options & RetainAsPublishedFlag) != 0, retainHandling);
    }
}
