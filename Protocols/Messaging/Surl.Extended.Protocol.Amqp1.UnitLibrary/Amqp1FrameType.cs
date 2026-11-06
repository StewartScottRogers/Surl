namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The frame type octet of AMQP 1.0 Part 2 section 2.3.1.
/// </summary>
public enum Amqp1FrameType : byte
{
    /// <summary>An AMQP frame (Part 2 section 2.3.2): a performative on a channel, and a payload after a transfer.</summary>
    Amqp = 0x00,

    /// <summary>A SASL frame (Part 5 section 5.3.1): a SASL performative; the channel is ignored.</summary>
    Sasl = 0x01,
}
