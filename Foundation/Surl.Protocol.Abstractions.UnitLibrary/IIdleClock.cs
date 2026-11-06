namespace Surl.Protocol.Abstractions;

/// <summary>
/// The exchange's idle clock, which the serving engine restarts whenever a byte moves and which
/// cancels the exchange after the idle timeout with none (ADR-0006, section 1). A server pauses it
/// while it holds the exchange on purpose - a command-stage fault delay (ADR-0124, decision 4;
/// ADR-0141) - so the hold is not taken for idleness, and restarts it when the hold ends.
/// </summary>
public interface IIdleClock
{
    /// <summary>
    /// Stops the idle clock until <see cref="Restart"/>. Calling it twice is harmless.
    /// </summary>
    void Pause();

    /// <summary>
    /// Starts the idle timeout counting again from now. Harmless after the exchange has ended.
    /// </summary>
    void Restart();
}
