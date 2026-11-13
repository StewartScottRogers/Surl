using Surl.Output;

namespace Surl.Composition;

/// <summary>
/// Where <c>LdapAccountSource</c>'s per-login notes go (ADR-0149 decision 9): the policy is composed before
/// the log exists, so the sink is handed to it first and given the log when serving starts. A note is
/// written from the verbose level up and only once a log is attached; concurrent logins share one log.
/// </summary>
internal sealed class DirectoryNoteSink
{
    private readonly object gate = new();
    private TextWriter? log;
    private LogLevel level = LogLevel.None;

    /// <summary>Starts writing notes to <paramref name="target"/> from <paramref name="logLevel"/>.</summary>
    /// <param name="target">The log stream.</param>
    /// <param name="logLevel">The log level.</param>
    public void Attach(TextWriter target, LogLevel logLevel)
    {
        ArgumentNullException.ThrowIfNull(target);
        lock (gate)
        {
            log = target;
            level = logLevel;
        }
    }

    /// <summary>Writes one note line when a log is attached and the level is verbose or above.</summary>
    /// <param name="line">The note.</param>
    public void Write(string line)
    {
        lock (gate)
        {
            if (log is not null && level >= LogLevel.Verbose)
            {
                log.WriteLine(line);
            }
        }
    }
}
