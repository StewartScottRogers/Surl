namespace Surl.Protocol.Abstractions;

/// <summary>
/// The default <see cref="ExchangeContext.IdleClock"/>: an exchange with no idle clock to pause,
/// as when a test serves a connection without the serving engine. Both members do nothing.
/// </summary>
public sealed class NoIdleClock : IIdleClock
{
    private NoIdleClock()
    {
    }

    /// <summary>
    /// The one instance.
    /// </summary>
    public static NoIdleClock Instance { get; } = new();

    /// <inheritdoc/>
    /// <remarks>Does nothing.</remarks>
    public void Pause()
    {
    }

    /// <inheritdoc/>
    /// <remarks>Does nothing.</remarks>
    public void Restart()
    {
    }
}
