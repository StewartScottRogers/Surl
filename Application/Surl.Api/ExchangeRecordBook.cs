using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// Builds <see cref="SurlExchangeRecord"/>s from an instance's exchange events, in stream order
/// (ADR-0099 decision 7, as ADR-0124 decision 1 amends it): an <see cref="ExchangeStartedEvent"/>
/// opens a record with its listen URL, scheme, client and start time, each
/// <see cref="ExchangeBytesEvent"/> adds one chunk, and an <see cref="ExchangeEndedEvent"/> closes
/// it. At most <paramref name="maxExchanges"/> records are kept, the oldest dropped and counted in
/// <see cref="DroppedExchangeCount"/> past it; at most <paramref name="maxBytesPerExchange"/> bytes
/// of each, both directions together, the rest cut and the record marked truncated. Every other event
/// of a kept exchange is noted in its <see cref="ExchangeFacts"/> for <see cref="FindExchanges"/>
/// (ADR-0143). Bytes or an end for an exchange it holds no open record of, and events of no kept
/// exchange, change no record.
/// </summary>
/// <remarks>Not safe for concurrent use: <see cref="ExchangeRecordSubscriber"/> calls it under its lock.</remarks>
/// <param name="maxExchanges">The most records kept; positive.</param>
/// <param name="maxBytesPerExchange">The most bytes kept of one exchange; positive.</param>
internal sealed class ExchangeRecordBook(int maxExchanges, int maxBytesPerExchange)
{
    private readonly Queue<ExchangeRecorder> kept = new();
    private readonly Dictionary<long, ExchangeRecorder> keptById = [];
    private readonly Dictionary<long, ExchangeRecorder> open = [];

    /// <summary>How many records were dropped, oldest first, to keep at most the cap.</summary>
    public long DroppedExchangeCount { get; private set; }

    /// <summary>Adds what <paramref name="surlEvent"/> says to the records.</summary>
    /// <param name="surlEvent">The next event, in stream order.</param>
    public void Apply(SurlEvent surlEvent)
    {
        switch (surlEvent)
        {
            case ExchangeStartedEvent started:
                Open(started);
                break;
            case ExchangeBytesEvent bytes:
                Keep(bytes);
                break;
            case ExchangeEndedEvent ended:
                open.Remove(ended.ExchangeId);
                NoteInItsExchange(ended);
                break;
            default:
                NoteInItsExchange(surlEvent);
                break;
        }
    }

    /// <summary>
    /// Marks every record not yet ended truncated: the stream dropped events for this subscriber, and
    /// any of them may have been one of those exchanges' bytes or its end.
    /// </summary>
    public void MarkOpenExchangesTruncated()
    {
        foreach (var recorder in open.Values)
        {
            recorder.MarkTruncated();
        }
    }

    /// <summary>The records kept, oldest first, as they stand now.</summary>
    /// <returns>A snapshot; events applied later do not change it.</returns>
    public IReadOnlyList<SurlExchangeRecord> Snapshot() => [.. kept.Select(recorder => recorder.Snapshot())];

    /// <summary>The kept records <paramref name="query"/> asks for, oldest first, as they stand now.</summary>
    /// <param name="query">The query.</param>
    /// <returns>A snapshot; events applied later do not change it.</returns>
    public IReadOnlyList<SurlExchangeRecord> FindExchanges(SurlEventQuery query) =>
        [.. kept.Where(recorder => recorder.Facts.Matches(query)).Select(recorder => recorder.Snapshot())];

    /// <summary>What is known of the kept exchange <paramref name="exchangeId"/>.</summary>
    /// <param name="exchangeId">An exchange ID, or <see langword="null"/>.</param>
    /// <returns>Its facts, or <see langword="null"/> when no record of it is kept.</returns>
    public ExchangeFacts? FactsOf(long? exchangeId) =>
        exchangeId is { } id && keptById.TryGetValue(id, out var recorder) ? recorder.Facts : null;

    private static SurlExchangeDirection ToSurlExchangeDirection(ExchangeDirection direction) =>
        direction == ExchangeDirection.Received ? SurlExchangeDirection.Received : SurlExchangeDirection.Sent;

    private void Keep(ExchangeBytesEvent bytes)
    {
        if (open.TryGetValue(bytes.ExchangeId, out var recorder))
        {
            recorder.Keep(ToSurlExchangeDirection(bytes.Direction), bytes.Bytes.Span);
        }
    }

    private void NoteInItsExchange(SurlEvent surlEvent) => FactsOf(SurlEventFacts.ExchangeIdOf(surlEvent))?.Note(surlEvent);

    private void Open(ExchangeStartedEvent started)
    {
        if (kept.Count == maxExchanges)
        {
            var dropped = kept.Dequeue();
            open.Remove(dropped.Facts.ExchangeId);
            keptById.Remove(dropped.Facts.ExchangeId);
            DroppedExchangeCount++;
        }

        var recorder = new ExchangeRecorder(new ExchangeFacts(started), maxBytesPerExchange);
        kept.Enqueue(recorder);
        keptById[started.ExchangeId] = recorder;
        open[started.ExchangeId] = recorder;
    }

    private sealed class ExchangeRecorder(ExchangeFacts facts, int maxBytes)
    {
        private readonly List<SurlExchangeChunk> chunks = [];
        private int keptByteCount;
        private bool isTruncated;

        public ExchangeFacts Facts => facts;

        public void Keep(SurlExchangeDirection direction, ReadOnlySpan<byte> bytes)
        {
            var keptLength = Math.Min(bytes.Length, maxBytes - keptByteCount);
            isTruncated |= keptLength < bytes.Length;
            if (keptLength > 0)
            {
                chunks.Add(new SurlExchangeChunk(direction, bytes[..keptLength].ToArray()));
                keptByteCount += keptLength;
            }
        }

        public void MarkTruncated() => isTruncated = true;

        public SurlExchangeRecord Snapshot() =>
            new(facts.ExchangeId, facts.ListenUrl, facts.Scheme, facts.Started.Client, facts.Started.At, [.. chunks], isTruncated);
    }
}
