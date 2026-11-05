namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// What <see cref="CoapMessageReader.Read"/> made of one datagram.
/// </summary>
public enum CoapReadOutcomeKind
{
    /// <summary>A whole CoAP message, in <see cref="CoapReadOutcome.Message"/>.</summary>
    Message,

    /// <summary>
    /// Not a CoAP version 1 message: shorter than the 4-byte header, or another version.
    /// It is silently ignored whatever its type (RFC 7252 section 3).
    /// </summary>
    NotCoap,

    /// <summary>
    /// A message format error after a readable header (sections 3 and 4.1): a CON is rejected
    /// with an RST of its Message ID, anything else is silently ignored (sections 4.2 and 4.3).
    /// </summary>
    FormatError,
}
