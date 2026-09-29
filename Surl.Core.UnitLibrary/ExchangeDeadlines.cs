namespace Surl.Core;

/// <summary>
/// One exchange's cancellation: at shutdown, after the idle timeout with no byte moving, and
/// at the maximum exchange duration, all counted on the engine's <see cref="TimeProvider"/>
/// (ADR-0006, sections 1 and 6).
/// </summary>
internal sealed class ExchangeDeadlines : IDisposable
{
    private readonly TimeSpan idleTimeout;
    private readonly CancellationToken shutdownToken;
    private readonly CancellationTokenSource? idle;
    private readonly CancellationTokenSource? duration;
    private readonly CancellationTokenSource anyReason;
    private readonly CancellationTokenRegistration firstReasonRecorder;
    private ExchangeCancellation? firstReason;

    /// <summary>
    /// Starts the exchange's clocks.
    /// </summary>
    /// <param name="limits">The idle timeout and maximum exchange duration; zero starts no clock.</param>
    /// <param name="timeProvider">The clock both are counted on.</param>
    /// <param name="shutdownToken">Cancelled when the engine gives up on every exchange.</param>
    public ExchangeDeadlines(ConnectionLimits limits, TimeProvider timeProvider, CancellationToken shutdownToken)
    {
        idleTimeout = limits.IdleTimeout;
        this.shutdownToken = shutdownToken;
        idle = StartClock(limits.IdleTimeout, timeProvider);
        duration = StartClock(limits.MaxExchangeDuration, timeProvider);
        anyReason = CancellationTokenSource.CreateLinkedTokenSource(
            shutdownToken, idle?.Token ?? CancellationToken.None, duration?.Token ?? CancellationToken.None);
        firstReasonRecorder = anyReason.Token.Register(() => firstReason ??= CurrentReason());
    }

    /// <summary>
    /// Cancelled for any of the three reasons: the exchange's
    /// <see cref="Protocol.Abstractions.ExchangeContext.CancellationToken"/>.
    /// </summary>
    public CancellationToken Token => anyReason.Token;

    /// <summary>
    /// Why <see cref="Token"/> was cancelled: the first reason that fired, even when another
    /// fired after it; <see langword="null"/> while it is not cancelled.
    /// </summary>
    /// <remarks>
    /// Read before the recorder has run, as an exchange that ends on the cancellation can,
    /// it is the reason that holds now, shutdown first.
    /// </remarks>
    public ExchangeCancellation? Reason => firstReason ?? CurrentReason();

    /// <summary>
    /// Restarts the idle clock, because a byte moved. Does nothing once disposed, as when a
    /// read the server left running completes after its exchange ended.
    /// </summary>
    public void RestartIdleClock()
    {
        try
        {
            idle?.CancelAfter(idleTimeout);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        firstReasonRecorder.Dispose();
        anyReason.Dispose();
        idle?.Dispose();
        duration?.Dispose();
    }

    private static CancellationTokenSource? StartClock(TimeSpan timeout, TimeProvider timeProvider) =>
        timeout == TimeSpan.Zero ? null : new CancellationTokenSource(timeout, timeProvider);

    private ExchangeCancellation? CurrentReason() =>
        shutdownToken.IsCancellationRequested ? ExchangeCancellation.Shutdown
        : idle?.IsCancellationRequested == true ? ExchangeCancellation.IdleTimeout
        : duration?.IsCancellationRequested == true ? ExchangeCancellation.MaxExchangeDuration
        : null;
}
