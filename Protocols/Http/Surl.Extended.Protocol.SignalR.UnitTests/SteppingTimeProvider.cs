namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock stands still until a test moves it forward.
/// </summary>
internal sealed class SteppingTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset now = utcNow;

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan by) => now += by;
}
