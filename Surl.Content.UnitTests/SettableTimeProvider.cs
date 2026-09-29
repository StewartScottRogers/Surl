namespace Surl.Content;

/// <summary>
/// A <see cref="TimeProvider"/> that reports the instant the test last set, so no test depends
/// on the wall clock.
/// </summary>
internal sealed class SettableTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
