namespace Surl.Protocol.Http;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock stands still, so a response's <c>Date</c> is known.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
