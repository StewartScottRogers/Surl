namespace Surl.Http3;

/// <summary>
/// A test clock a test moves by hand, so <see cref="Http3TestQuicClient.FinishAsync"/> can move it
/// past a closed QUIC connection's closing or draining state (RFC 9000 section 10.2) instead of
/// waiting for it.
/// </summary>
internal interface IMovableClock
{
    /// <summary>Moves the clock on by <paramref name="by"/> and fires every timer due by then.</summary>
    void Advance(TimeSpan by);
}
