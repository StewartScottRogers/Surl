namespace Surl.Quic;

/// <summary>A clock that stands still until a test moves it.</summary>
internal sealed class UnitTestTimeProvider : TimeProvider
{
    private DateTimeOffset now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan by) => now += by;
}
