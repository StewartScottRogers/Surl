using Surl.Protocol.Abstractions;

namespace Surl.Control;

/// <summary>
/// Builds <c>GET /v1/exchanges</c>'s answer from the held events, in ADR-0103 decision 3's <c>get_exchanges</c>
/// shape with each exchange's listen URL and scheme: oldest first, at most <see cref="MaxShownBytesPerExchange"/>
/// bytes of each exchange and <see cref="MaxShownBytesPerAnswer"/> in all, a cut ending a chunk early and dropping
/// the chunks after it.
/// </summary>
/// <remarks>
/// An exchange is listed while its start is held. <c>is_truncated</c> says the endpoint's subscription skipped
/// events while the exchange ran, so some of its bytes may be missing.
/// </remarks>
internal static class ControlExchangeWriter
{
    /// <summary>The most bytes of one exchange shown.</summary>
    public const int MaxShownBytesPerExchange = 65536;

    /// <summary>The most bytes shown in one answer.</summary>
    public const int MaxShownBytesPerAnswer = 1_048_576;

    /// <summary>The exchanges after <paramref name="afterExchangeId"/> that match, at most <paramref name="limit"/>.</summary>
    /// <param name="view">What the log holds.</param>
    /// <param name="afterExchangeId">Only exchanges with a larger ID.</param>
    /// <param name="scheme">Only exchanges of this scheme, or <see langword="null"/>.</param>
    /// <param name="listenUrl">Only exchanges on this listen URL, or <see langword="null"/>.</param>
    /// <param name="limit">The most exchanges listed.</param>
    /// <returns>The answer.</returns>
    public static ControlExchangesJson Write(ControlEventLogView view, long afterExchangeId, string? scheme, string? listenUrl, int limit)
    {
        var matching = view.Events
            .OfType<ExchangeStartedEvent>()
            .Where(started => IsAskedFor(started, afterExchangeId, scheme, listenUrl))
            .OrderBy(started => started.ExchangeId)
            .ToArray();
        var exchanges = new List<ControlExchangeJson>();
        var remainingBytes = MaxShownBytesPerAnswer;
        foreach (var started in matching)
        {
            if (exchanges.Count == limit || remainingBytes == 0)
            {
                break;
            }

            var exchange = WriteExchange(view, started, Math.Min(MaxShownBytesPerExchange, remainingBytes));
            remainingBytes -= (int)exchange.ShownBytes;
            exchanges.Add(exchange);
        }

        return new ControlExchangesJson(exchanges, view.DroppedExchangeCount, exchanges.Count < matching.Length);
    }

    private static bool IsAskedFor(ExchangeStartedEvent started, long afterExchangeId, string? scheme, string? listenUrl) =>
        started.ExchangeId > afterExchangeId
        && (scheme is null || string.Equals(scheme, started.Scheme, StringComparison.OrdinalIgnoreCase))
        && (listenUrl is null || listenUrl == started.ListenUrl);

    private static ControlExchangeJson WriteExchange(ControlEventLogView view, ExchangeStartedEvent started, int maxShownBytes)
    {
        var events = view.Events.Where(surlEvent => ControlEventKinds.ExchangeIdOf(surlEvent) == started.ExchangeId).ToArray();
        var chunks = events.OfType<ExchangeBytesEvent>().ToArray();
        var shown = new List<ControlExchangeChunkJson>();
        var shownBytes = 0;
        var isCut = false;
        foreach (var chunk in chunks)
        {
            var bytes = ControlShownBytes.Show(chunk.Bytes.Span, maxShownBytes - shownBytes);
            isCut = bytes.ShownLength < chunk.Bytes.Length;
            if (bytes.ShownLength > 0)
            {
                shown.Add(new ControlExchangeChunkJson(ControlEventKinds.SnakeCase(chunk.Direction), bytes.Encoding, bytes.Data));
                shownBytes += bytes.ShownLength;
            }

            if (isCut)
            {
                break;
            }
        }

        var lastSequence = events.OfType<ExchangeEndedEvent>().Select(ended => ended.Sequence).FirstOrDefault(long.MaxValue);

        return new ControlExchangeJson(
            started.ExchangeId,
            started.ListenUrl,
            started.Scheme,
            $"{started.Client}",
            ControlEventWriter.Timestamp(started.At),
            shown,
            chunks.Sum(chunk => (long)chunk.Bytes.Length),
            shownBytes,
            isCut,
            view.HasGapBetween(started.Sequence, lastSequence));
    }
}
