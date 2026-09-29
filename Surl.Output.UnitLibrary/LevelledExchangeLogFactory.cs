using System.Globalization;
using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Output;

/// <summary>
/// The exchange log at a chosen <see cref="LogLevel"/> (ADR-0033, sections 1, 3 and 5): hands
/// out one <see cref="IExchangeLog"/> per exchange that writes, to one shared writer, nothing
/// (<see cref="LogLevel.None"/>), the notes of a protocol server that threw
/// (<see cref="LogLevel.Error"/>), those and the engine's open, cancel and TLS-failure notes
/// (<see cref="LogLevel.Info"/>), or every event (<see cref="LogLevel.Verbose"/>), and, with
/// timestamps on, stamps each line with the local time of an injected clock.
/// </summary>
/// <remarks>
/// Every line is the verbose log's line for its event (ADR-0007, section 8), written with
/// one call under a lock the factory's logs share, so lines from concurrent exchanges never
/// interleave. The <see cref="LogLevel.Trace"/> dump is not written here but by
/// <see cref="TraceExchangeLogFactory"/> (ADR-0033, section 4).
/// </remarks>
public sealed class LevelledExchangeLogFactory : IExchangeLogFactory
{
    private readonly TextWriter writer;
    private readonly LogLevel level;
    private readonly TimeProvider? timestampClock;
    private readonly Lock writeLock = new();

    /// <summary>
    /// Creates the factory.
    /// </summary>
    /// <param name="writer">Where the log goes: the log stream (ADR-0033, section 1).</param>
    /// <param name="level">The level; any but <see cref="LogLevel.Trace"/>.</param>
    /// <param name="stampTimes">Whether <c>--trace-time</c> was given: each line then starts with the local time it was written.</param>
    /// <param name="timeProvider">The clock whose <see cref="TimeProvider.GetLocalNow"/> stamps the lines.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is <see cref="LogLevel.Trace"/> or no level.</exception>
    public LevelledExchangeLogFactory(TextWriter writer, LogLevel level, bool stampTimes, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (level is < LogLevel.None or >= LogLevel.Trace)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "The trace dump is not an exchange log this factory writes.");
        }

        this.writer = writer;
        this.level = level;
        timestampClock = stampTimes ? timeProvider : null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The remote endpoint is not written here: the engine's <c>Exchange &lt;id&gt; opened</c>
    /// note names it (ADR-0033, section 3).
    /// </remarks>
    public IExchangeLog Create(long exchangeId, EndPoint remoteEndPoint)
    {
        ArgumentNullException.ThrowIfNull(remoteEndPoint);

        if (level == LogLevel.None)
        {
            return SilentExchangeLog.Instance;
        }

        var lineWriter = new VerboseExchangeLog(exchangeId, writer, writeLock, timestampClock);
        return level == LogLevel.Verbose ? lineWriter : new NoteFilteringExchangeLog(lineWriter, WrittenNotePrefixes(exchangeId));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// At <see cref="LogLevel.Info"/> and <see cref="LogLevel.Verbose"/>, writes
    /// <c>#- * &lt;text&gt;</c>, escaped like any note: <c>-</c> stands where the exchange id
    /// goes, since exchange ids start at 1 (ADR-0028). At <see cref="LogLevel.None"/> and
    /// <see cref="LogLevel.Error"/>, writes nothing.
    /// </remarks>
    public void NoteOutsideExchange(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (level >= LogLevel.Info)
        {
            VerboseExchangeLog.OutsideAnyExchange(writer, writeLock, timestampClock).Note(text);
        }
    }

    private string[] WrittenNotePrefixes(long exchangeId)
    {
        var exchange = "Exchange " + exchangeId.ToString(CultureInfo.InvariantCulture);
        var threw = exchange + " ended because the protocol server threw ";
        return level == LogLevel.Error
            ? [threw]
            : [exchange + " opened: ", exchange + " cancelled", "TLS handshake failed: ", threw];
    }
}
