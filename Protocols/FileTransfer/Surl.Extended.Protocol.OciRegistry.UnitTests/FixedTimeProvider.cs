namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock stands still until a test moves it, so an answer's
/// <c>Date</c> is known and an upload session's expiry is reached on purpose.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
