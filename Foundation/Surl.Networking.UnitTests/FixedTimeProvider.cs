namespace Surl.Networking;

/// <summary>
/// A <see cref="TimeProvider"/> that always reports the same instant, so no test depends on
/// the wall clock (ADR-0010, section 6).
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
