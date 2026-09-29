namespace Surl.Core;

/// <summary>
/// Why the serving engine cancelled an exchange.
/// </summary>
internal enum ExchangeCancellation
{
    /// <summary>
    /// The engine is shutting down and the grace period ran out.
    /// </summary>
    Shutdown,

    /// <summary>
    /// No byte moved for the idle timeout.
    /// </summary>
    IdleTimeout,

    /// <summary>
    /// The exchange reached the maximum exchange duration.
    /// </summary>
    MaxExchangeDuration,
}
