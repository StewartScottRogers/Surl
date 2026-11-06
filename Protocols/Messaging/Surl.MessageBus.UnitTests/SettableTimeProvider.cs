namespace Surl.MessageBus;

/// <summary>
/// A clock that reads whatever time a test last set.
/// </summary>
internal sealed class SettableTimeProvider : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 4, 8, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
