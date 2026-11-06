namespace Surl.Conformance;

/// <summary>
/// A clock stopped at one instant.
/// </summary>
internal sealed class UnitTestFixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
