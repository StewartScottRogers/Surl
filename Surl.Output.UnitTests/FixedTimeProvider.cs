namespace Surl.Output;

/// <summary>
/// A clock stopped at a given instant, in a fixed time zone, that counts its reads.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow, TimeSpan utcOffset) : TimeProvider
{
    private DateTimeOffset now = utcNow;

    public int Reads { get; private set; }

    public override TimeZoneInfo LocalTimeZone { get; } =
        TimeZoneInfo.CreateCustomTimeZone("Surl test zone", utcOffset, "Surl test zone", "Surl test zone");

    public override DateTimeOffset GetUtcNow()
    {
        Reads++;
        return now;
    }

    public void Advance(TimeSpan by) => now += by;
}
