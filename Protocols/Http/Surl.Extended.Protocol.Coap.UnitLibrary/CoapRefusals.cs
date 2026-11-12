using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The refusals ADR-0097 decision 7 answers in CoAP's own words: <c>5.03 Service Unavailable</c> with
/// Max-Age 5 (RFC 7252 section 5.9.3.4) for a flow past a connection limit or past its requests in
/// progress, and <c>4.13 Request Entity Too Large</c> with Size1 and a Block1 hint (RFC 7959 section
/// 2.9.3) for a datagram past <c>--max-message</c>.
/// </summary>
internal static class CoapRefusals
{
    /// <summary>The Max-Age a refusing <c>5.03</c> carries: retry after 5 seconds.</summary>
    public const uint RetryAfterSeconds = 5;

    /// <summary>The most requests one flow may have in progress (ADR-0097 decision 7).</summary>
    public const int RequestsInProgressPerFlow = 8;

    /// <summary>The bytes of header and options a Block1 hint leaves room for beside its block.</summary>
    private const int HeaderAndOptionsBytes = 128;

    /// <summary>
    /// A <c>5.03</c> ACK that piggybacks on <paramref name="request"/>, a CON.
    /// </summary>
    /// <param name="request">The refused CON request.</param>
    /// <returns>The ACK.</returns>
    public static CoapMessage ServiceUnavailableAck(CoapMessage request) =>
        new(
            CoapMessageType.Acknowledgement,
            CoapCode.ServiceUnavailable,
            request.MessageId,
            request.Token,
            [CoapOption.FromUInt(CoapOptionNumber.MaxAge, RetryAfterSeconds)]);

    /// <summary>
    /// A <c>5.03</c> NON answering <paramref name="request"/>, a NON.
    /// </summary>
    /// <param name="request">The refused NON request.</param>
    /// <param name="messageId">The Message ID of the answer.</param>
    /// <returns>The NON.</returns>
    public static CoapMessage ServiceUnavailableNon(CoapMessage request, ushort messageId) =>
        new(
            CoapMessageType.NonConfirmable,
            CoapCode.ServiceUnavailable,
            messageId,
            request.Token,
            [CoapOption.FromUInt(CoapOptionNumber.MaxAge, RetryAfterSeconds)]);

    /// <summary>
    /// A <c>4.13</c> ACK for a CON past <c>--max-message</c>: Size1 is <c>--max-filesize</c> when one
    /// is set, and Block1 names the largest block size whose block and 128 bytes of header and options
    /// fit in the limit, when one does.
    /// </summary>
    /// <param name="request">The refused CON request.</param>
    /// <param name="limits">The exchange's limits.</param>
    /// <returns>The ACK.</returns>
    public static CoapMessage TooLargeAck(CoapMessage request, ExchangeLimits limits)
    {
        List<CoapOption> options = [];

        if (LargestBlockSizeExponent(limits.MaxMessageBytes) is { } exponent)
        {
            options.Add(new CoapBlockOption(0, false, exponent).ToOption(CoapOptionNumber.Block1));
        }

        if (limits.MaxUploadBytes > 0)
        {
            options.Add(CoapOption.FromUInt(CoapOptionNumber.Size1, (uint)Math.Min(limits.MaxUploadBytes, uint.MaxValue)));
        }

        return new CoapMessage(CoapMessageType.Acknowledgement, CoapCode.RequestEntityTooLarge, request.MessageId, request.Token, options);
    }

    private static int? LargestBlockSizeExponent(long maxMessageBytes)
    {
        for (var exponent = CoapBlockOption.LargestSizeExponent; exponent >= 0; exponent--)
        {
            if (CoapBlockOption.SizeOf(exponent) + HeaderAndOptionsBytes <= maxMessageBytes)
            {
                return exponent;
            }
        }

        return null;
    }
}
