namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// How a CON message the server sent ended (RFC 7252 sections 4.2 and 5.2.2): acknowledged,
/// reset, or given up after MAX_RETRANSMIT retransmissions.
/// </summary>
internal enum CoapDelivery
{
    /// <summary>A matching ACK arrived.</summary>
    Acknowledged,

    /// <summary>A matching Empty RST arrived: the client has no context for the message.</summary>
    Reset,

    /// <summary>No matching ACK or RST arrived before the last retransmission timed out.</summary>
    GivenUp,
}
