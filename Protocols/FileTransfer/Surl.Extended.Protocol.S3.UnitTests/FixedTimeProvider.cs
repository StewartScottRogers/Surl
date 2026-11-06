namespace Surl.Extended.Protocol.S3;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock stands still until a test moves it, so an answer's
/// <c>Date</c> is known and an upload's idleness can be measured.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset now = utcNow;

    public override DateTimeOffset GetUtcNow() => now;

    /// <summary>
    /// Moves the clock on by <paramref name="by"/>.
    /// </summary>
    public void Advance(TimeSpan by) => now += by;
}
