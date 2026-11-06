namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The CoAP server's one random source (ADR-0097 decision 0): each flow's first Message ID, the
/// ACK_RANDOM_FACTOR draw, and the names of resources a <c>POST</c> creates.
/// </summary>
internal interface ICoapRandomSource
{
    /// <summary>Draws a uniformly random 16-bit Message ID.</summary>
    /// <returns>The Message ID.</returns>
    ushort DrawMessageId();

    /// <summary>Draws a uniformly random fraction, at least 0 and less than 1.</summary>
    /// <returns>The fraction.</returns>
    double DrawFraction();

    /// <summary>Fills <paramref name="bytes"/> with random bytes.</summary>
    /// <param name="bytes">The bytes to fill.</param>
    void Fill(Span<byte> bytes);
}
