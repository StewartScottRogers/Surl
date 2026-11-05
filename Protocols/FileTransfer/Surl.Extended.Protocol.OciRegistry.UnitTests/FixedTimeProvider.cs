namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock stands still, so an answer's <c>Date</c> is known.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
