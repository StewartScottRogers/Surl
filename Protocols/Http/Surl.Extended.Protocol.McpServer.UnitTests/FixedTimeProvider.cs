namespace Surl.Extended.Protocol.McpServer;

/// <summary>A <see cref="TimeProvider" /> whose clock stands still until a test moves it.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
