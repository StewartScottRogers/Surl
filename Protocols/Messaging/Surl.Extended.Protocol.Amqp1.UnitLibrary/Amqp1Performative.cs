namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// A performative: the described list at the start of a frame body, one of the nine of AMQP 1.0
/// Part 2 section 2.7 carried in AMQP frames, or one of the five of Part 5 section 5.3.3 carried in
/// SASL frames.
/// </summary>
public abstract record Amqp1Performative : Amqp1DescribedList
{
    /// <summary>The frame type that carries this performative: <see cref="Amqp1FrameType.Sasl"/> for the SASL ones.</summary>
    public Amqp1FrameType FrameType => DescriptorCode >= Amqp1Descriptor.SaslMechanisms ? Amqp1FrameType.Sasl : Amqp1FrameType.Amqp;
}
