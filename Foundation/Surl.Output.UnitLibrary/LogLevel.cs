namespace Surl.Output;

/// <summary>
/// How much <c>surl</c> logs (ADR-0033, section 1). Each level writes everything the one
/// before it writes, except that the per-exchange lines are written in the chosen level's
/// form only: the info line, the verbose lines or the dump.
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// <c>-s</c>: nothing.
    /// </summary>
    None,

    /// <summary>
    /// <c>-s -S</c>: only the notes of a protocol server that threw.
    /// </summary>
    Error,

    /// <summary>
    /// The default: one line when each exchange opens, and the engine's notes that tell of
    /// trouble (ADR-0033, section 3).
    /// </summary>
    Info,

    /// <summary>
    /// <c>-v</c>: ADR-0007 section 8's <c>#&lt;id&gt; &lt;marker&gt; &lt;text&gt;</c> line for every event.
    /// </summary>
    Verbose,

    /// <summary>
    /// <c>--trace</c> and <c>--trace-ascii</c>: a dump of every byte (ADR-0033, section 4).
    /// </summary>
    Trace,
}
