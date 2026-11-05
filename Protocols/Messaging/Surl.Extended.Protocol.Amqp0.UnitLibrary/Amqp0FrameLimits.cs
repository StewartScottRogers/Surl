namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The frame-size limits of ADR-0112 decisions 3 and 8.
/// </summary>
public static class Amqp0FrameLimits
{
    /// <summary>
    /// The XML's <c>frame-min-size</c>: 4096. Every frame before <c>tune-ok</c> may be this large
    /// and no larger, and no frame-max is offered or accepted below it.
    /// </summary>
    public const int FrameMinSize = 4096;

    /// <summary>The largest frame-max the broker offers in <c>connection.tune</c>: 131072.</summary>
    public const int LargestFrameMaxOffered = 131072;

    /// <summary>
    /// The frame-max offered in <c>connection.tune</c>: <see cref="LargestFrameMaxOffered"/>, or
    /// <paramref name="maxMessageBytes"/> (<c>--max-message</c>) when smaller, never below
    /// <see cref="FrameMinSize"/>.
    /// </summary>
    /// <param name="maxMessageBytes">ADR-0006's <c>--max-message</c>; 0 means no limit.</param>
    /// <returns>The frame-max to offer.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxMessageBytes"/> is negative.</exception>
    public static int OfferedFrameMax(long maxMessageBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxMessageBytes);

        if (maxMessageBytes == 0)
        {
            return LargestFrameMaxOffered;
        }

        return (int)Math.Clamp(maxMessageBytes, FrameMinSize, LargestFrameMaxOffered);
    }
}
