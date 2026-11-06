using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Output;

/// <summary>
/// The <see cref="LogLevel.Trace"/> exchange log (ADR-0033, section 4): hands out one
/// <see cref="IExchangeLog"/> per exchange that dumps every byte it receives and sends, in
/// the <c>--trace</c> or <c>--trace-ascii</c> layout, and writes its notes, to one shared
/// writer, and, with timestamps on, stamps each header and note line with the local time of
/// an injected clock (section 5).
/// </summary>
/// <remarks>
/// Each event is written with one call under a lock the factory's logs share, so the dumps
/// of concurrent exchanges never interleave inside one event block.
/// </remarks>
public sealed class TraceExchangeLogFactory : IExchangeLogFactory
{
    private readonly TextWriter writer;
    private readonly TraceDumpLayout layout;
    private readonly TimeProvider? timestampClock;
    private readonly Lock writeLock = new();

    /// <summary>
    /// Creates the factory.
    /// </summary>
    /// <param name="writer">Where the dump goes: the trace file, stdout for <c>-</c>, or the log stream.</param>
    /// <param name="layout">The layout: <see cref="TraceDumpLayout.HexAndAscii"/> for <c>--trace</c>, <see cref="TraceDumpLayout.Ascii"/> for <c>--trace-ascii</c>.</param>
    /// <param name="stampTimes">Whether <c>--trace-time</c> was given: each header and note line then starts with the local time it was written.</param>
    /// <param name="timeProvider">The clock whose <see cref="TimeProvider.GetLocalNow"/> stamps the lines.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="layout"/> is no layout.</exception>
    public TraceExchangeLogFactory(TextWriter writer, TraceDumpLayout layout, bool stampTimes, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (layout is not (TraceDumpLayout.HexAndAscii or TraceDumpLayout.Ascii))
        {
            throw new ArgumentOutOfRangeException(nameof(layout), layout, "No such trace dump layout.");
        }

        this.writer = writer;
        this.layout = layout;
        timestampClock = stampTimes ? timeProvider : null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The remote endpoint is not written here: the engine's <c>Exchange &lt;id&gt; opened</c>
    /// note names it.
    /// </remarks>
    public IExchangeLog Create(long exchangeId, EndPoint remoteEndPoint)
    {
        ArgumentNullException.ThrowIfNull(remoteEndPoint);

        return new TraceExchangeLog(exchangeId, layout, writer, writeLock, timestampClock);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Writes <c>#- * &lt;text&gt;</c>, escaped like any note (ADR-0028).
    /// </remarks>
    public void NoteOutsideExchange(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        VerboseExchangeLog.OutsideAnyExchange(writer, writeLock, timestampClock).Note(text);
    }
}
