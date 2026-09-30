namespace Surl.Console;

/// <summary>
/// A clock stopped at a given instant, in a fixed time zone; its timers are the system's.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow, TimeSpan utcOffset) : TimeProvider
{
    public override TimeZoneInfo LocalTimeZone { get; } =
        TimeZoneInfo.CreateCustomTimeZone("Surl test zone", utcOffset, "Surl test zone", "Surl test zone");

    public override DateTimeOffset GetUtcNow() => utcNow;
}
