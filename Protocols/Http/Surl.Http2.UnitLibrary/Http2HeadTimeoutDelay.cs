namespace Surl.Http2;

/// <summary>
/// The delay a header block's head-timeout timer is started with, as <c>Surl.HttpMessage</c>'s
/// <c>HttpConnectionReader</c> starts its own: a timer cannot wait longer than
/// <see cref="uint.MaxValue"/> - 1 milliseconds (about 49.7 days), so a head timeout past that
/// never fires in practice and is treated as none.
/// </summary>
internal static class Http2HeadTimeoutDelay
{
    private static readonly TimeSpan MaxTimerDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1.0);

    /// <summary>
    /// The timer delay for <paramref name="headTimeout"/>.
    /// </summary>
    /// <param name="headTimeout">The head timeout, <see cref="Timeout.InfiniteTimeSpan"/> for none.</param>
    /// <returns>The head timeout, or <see cref="Timeout.InfiniteTimeSpan"/> when it is longer than a timer can wait.</returns>
    public static TimeSpan For(TimeSpan headTimeout) =>
        headTimeout > MaxTimerDelay ? Timeout.InfiniteTimeSpan : headTimeout;
}
