namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The frame-size limits of ADR-0107 decisions 2, 3 and 7.
/// </summary>
public static class Amqp1FrameLimits
{
    /// <summary>
    /// MIN-MAX-FRAME-SIZE (Part 2 section 2.7.1): 512. Every frame before <c>open</c>, and every
    /// SASL frame, may be this large and no larger, and no <c>max-frame-size</c> is offered or
    /// accepted below it.
    /// </summary>
    public const uint MinMaxFrameSize = 512;

    /// <summary>The largest <c>max-frame-size</c> the container offers in its <c>open</c>: 65536.</summary>
    public const uint LargestMaxFrameSizeOffered = 65536;

    /// <summary>
    /// The <c>max-frame-size</c> the container offers: <see cref="LargestMaxFrameSizeOffered"/>,
    /// or <paramref name="maxMessageBytes"/> (<c>--max-message</c>) when smaller, never below
    /// <see cref="MinMaxFrameSize"/>.
    /// </summary>
    /// <param name="maxMessageBytes">ADR-0006's <c>--max-message</c>; 0 means no limit.</param>
    /// <returns>The <c>max-frame-size</c> to offer.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxMessageBytes"/> is negative.</exception>
    public static uint OfferedMaxFrameSize(long maxMessageBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxMessageBytes);

        if (maxMessageBytes == 0)
        {
            return LargestMaxFrameSizeOffered;
        }

        return (uint)Math.Clamp(maxMessageBytes, MinMaxFrameSize, LargestMaxFrameSizeOffered);
    }
}
