namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// RFC 7252 section 4.8's transmission parameters at their defaults, and the server's own message-layer
/// bounds (ADR-0097 decisions 2 and 7). None has an option: section 4.8.1 forbids lowering ACK_TIMEOUT
/// without congestion-control measurements the server does not keep.
/// </summary>
internal static class CoapTransmissionParameters
{
    /// <summary>ACK_TIMEOUT, 2 seconds.</summary>
    public static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(2);

    /// <summary>ACK_RANDOM_FACTOR, 1.5.</summary>
    public const double AckRandomFactor = 1.5;

    /// <summary>MAX_RETRANSMIT, 4.</summary>
    public const int MaxRetransmit = 4;

    /// <summary>EXCHANGE_LIFETIME, 247 seconds (section 4.8.2): how long a CON's answer is remembered.</summary>
    public static readonly TimeSpan ExchangeLifetime = TimeSpan.FromSeconds(247);

    /// <summary>NON_LIFETIME, 145 seconds (section 4.8.2): how long a NON's Message ID is remembered.</summary>
    public static readonly TimeSpan NonLifetime = TimeSpan.FromSeconds(145);

    /// <summary>
    /// How long a CON request waits for its answer before an Empty ACK goes and the answer follows
    /// separately: 1 second, half ACK_TIMEOUT, so before the client's first retransmission.
    /// </summary>
    public static readonly TimeSpan SeparateResponseAfter = TimeSpan.FromSeconds(1);

    /// <summary>The most Message IDs a flow's duplicate cache remembers (ADR-0097 decision 7).</summary>
    public const int DuplicateCacheCapacity = 128;

    /// <summary>
    /// The first retransmission timeout for a draw of <paramref name="fraction"/> in [0, 1): between
    /// ACK_TIMEOUT and ACK_TIMEOUT times ACK_RANDOM_FACTOR (section 4.2).
    /// </summary>
    /// <param name="fraction">A uniformly random fraction.</param>
    /// <returns>The timeout.</returns>
    public static TimeSpan InitialTimeout(double fraction) =>
        AckTimeout * (1 + ((AckRandomFactor - 1) * fraction));
}
