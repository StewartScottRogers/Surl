namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The two-bit Type field of a CoAP message header (RFC 7252 section 3).
/// </summary>
public enum CoapMessageType
{
    /// <summary>CON, 0: a message that needs an acknowledgement (section 4.2).</summary>
    Confirmable = 0,

    /// <summary>NON, 1: a message that needs none (section 4.3).</summary>
    NonConfirmable = 1,

    /// <summary>ACK, 2: acknowledges a CON with its Message ID (section 4.2).</summary>
    Acknowledgement = 2,

    /// <summary>RST, 3: rejects a message the receiver lacks context for (sections 4.2 and 4.3).</summary>
    Reset = 3,
}
