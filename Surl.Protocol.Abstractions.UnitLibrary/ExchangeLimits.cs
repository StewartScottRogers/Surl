namespace Surl.Protocol.Abstractions;

/// <summary>
/// The hardening limits every protocol server reads from <see cref="ExchangeContext.Limits"/>
/// (ADR-0006, sections 1 and 6).
/// </summary>
/// <remarks>
/// Idle timeout and maximum exchange duration are deliberately not here: <c>Surl.Core</c>
/// enforces them by cancelling <see cref="ExchangeContext.CancellationToken"/> (ADR-0006,
/// section 6). Every value is checked when it is set, by the constructor or by a
/// <see langword="with"/> expression, and an out-of-range one throws
/// <see cref="ArgumentOutOfRangeException"/> naming the property.
/// </remarks>
public sealed record ExchangeLimits
{
    /// <summary>
    /// ADR-0006 section 1's defaults: a 30-second head timeout, a 102400-byte request head,
    /// an 8192-byte line, a 1048576-byte message and a 104857600-byte upload.
    /// </summary>
    public static ExchangeLimits Default { get; } = new(
        TimeSpan.FromSeconds(30),
        maxRequestHeadBytes: 102400,
        maxLineBytes: 8192,
        maxMessageBytes: 1048576,
        maxUploadBytes: 104857600);

    /// <summary>
    /// Creates a set of limits (ADR-0006, section 6).
    /// </summary>
    /// <param name="headTimeout">The value of <see cref="HeadTimeout"/>.</param>
    /// <param name="maxRequestHeadBytes">The value of <see cref="MaxRequestHeadBytes"/>.</param>
    /// <param name="maxLineBytes">The value of <see cref="MaxLineBytes"/>.</param>
    /// <param name="maxMessageBytes">The value of <see cref="MaxMessageBytes"/>.</param>
    /// <param name="maxUploadBytes">The value of <see cref="MaxUploadBytes"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A size is negative, or <paramref name="headTimeout"/> is negative and not
    /// <see cref="Timeout.InfiniteTimeSpan"/>.
    /// </exception>
    public ExchangeLimits(
        TimeSpan headTimeout,
        long maxRequestHeadBytes,
        long maxLineBytes,
        long maxMessageBytes,
        long maxUploadBytes)
    {
        HeadTimeout = headTimeout;
        MaxRequestHeadBytes = maxRequestHeadBytes;
        MaxLineBytes = maxLineBytes;
        MaxMessageBytes = maxMessageBytes;
        MaxUploadBytes = maxUploadBytes;
    }

    /// <summary>
    /// How long a client has to send a complete request head (ADR-0006, section 1).
    /// <see cref="Timeout.InfiniteTimeSpan"/> means no limit.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is negative and not <see cref="Timeout.InfiniteTimeSpan"/>.
    /// </exception>
    public TimeSpan HeadTimeout
    {
        get;
        init
        {
            if (value < TimeSpan.Zero && value != Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(HeadTimeout),
                    value,
                    "The head timeout must be zero or more, or Timeout.InfiniteTimeSpan.");
            }

            field = value;
        }
    }

    /// <summary>
    /// The most bytes a request head may hold (ADR-0006, section 1). 0 means no limit.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public long MaxRequestHeadBytes
    {
        get;
        init => field = RequireSize(value, nameof(MaxRequestHeadBytes));
    }

    /// <summary>
    /// The most bytes one protocol line may hold (ADR-0006, section 1). 0 means no limit.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public long MaxLineBytes
    {
        get;
        init => field = RequireSize(value, nameof(MaxLineBytes));
    }

    /// <summary>
    /// The most bytes one protocol message may hold (ADR-0006, section 1). 0 means no limit.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public long MaxMessageBytes
    {
        get;
        init => field = RequireSize(value, nameof(MaxMessageBytes));
    }

    /// <summary>
    /// The most bytes one upload may hold (ADR-0006, section 1). 0 means no limit.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public long MaxUploadBytes
    {
        get;
        init => field = RequireSize(value, nameof(MaxUploadBytes));
    }

    private static long RequireSize(long value, string propertyName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, propertyName);
        return value;
    }
}
