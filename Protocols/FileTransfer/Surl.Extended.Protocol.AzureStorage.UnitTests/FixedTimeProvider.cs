namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock stands still until a test moves it, so an answer's
/// <c>Date</c> is known.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    /// <summary>
    /// The time the clock stands at.
    /// </summary>
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
