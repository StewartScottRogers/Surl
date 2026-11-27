namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The frame-size limits of ADR-0107 decisions 2, 3 and 7 and its BL-1884 amendment.
/// </summary>
public static class Amqp1FrameLimits
{
    /// <summary>
    /// MIN-MAX-FRAME-SIZE (Part 2 section 2.7.1): 512. The <c>open</c> frame may be this large
    /// and no larger, and no <c>max-frame-size</c> is offered or accepted below it.
    /// </summary>
    public const uint MinMaxFrameSize = 512;

    /// <summary>
    /// The largest <c>max-frame-size</c> the container offers in its <c>open</c>, and so the
    /// largest SASL frame it ever reads: 65536.
    /// </summary>
    public const uint LargestMaxFrameSizeOffered = 65536;

    /// <summary>
    /// The largest SASL frame the container reads: the <c>max-frame-size</c> it would offer
    /// (<see cref="OfferedMaxFrameSize"/>). Part 5 section 5.3.1 limits a SASL frame to
    /// <see cref="MinMaxFrameSize"/>, which a Kerberos ticket in <c>sasl-init</c> does not fit,
    /// so more is read, as widely used peers do (ADR-0107's BL-1884 amendment).
    /// </summary>
    /// <param name="maxMessageBytes">ADR-0006's <c>--max-message</c>; 0 means no limit.</param>
    /// <returns>The largest SASL frame to read, header included.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxMessageBytes"/> is negative.</exception>
    public static uint LargestSaslFrameSizeRead(long maxMessageBytes) => OfferedMaxFrameSize(maxMessageBytes);

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
