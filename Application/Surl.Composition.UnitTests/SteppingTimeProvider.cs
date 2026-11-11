namespace Surl.Composition;

/// <summary>
/// A clock that stands still until <see cref="Step"/> is set, and then moves on by it at every
/// read of the time, so a test can make a deadline pass between two reads.
/// </summary>
internal sealed class SteppingTimeProvider : TimeProvider
{
    private readonly Lock gate = new();
    private DateTimeOffset utcNow = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);

    /// <summary>How far the clock moves after each read.</summary>
    public TimeSpan Step { get; set; }

    public override DateTimeOffset GetUtcNow()
    {
        lock (gate)
        {
            var now = utcNow;
            utcNow += Step;
            return now;
        }
    }
}
