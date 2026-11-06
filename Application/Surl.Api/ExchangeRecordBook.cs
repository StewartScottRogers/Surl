using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// Builds <see cref="SurlExchangeRecord"/>s from an instance's exchange events, in stream order
/// (ADR-0099 decision 7, as ADR-0124 decision 1 amends it): an <see cref="ExchangeStartedEvent"/>
/// opens a record with its listen URL, scheme, client and start time, each
/// <see cref="ExchangeBytesEvent"/> adds one chunk, and an <see cref="ExchangeEndedEvent"/> closes
/// it. At most <paramref name="maxExchanges"/> records are kept, the oldest dropped and counted in
/// <see cref="DroppedExchangeCount"/> past it; at most <paramref name="maxBytesPerExchange"/> bytes
/// of each, both directions together, the rest cut and the record marked truncated. Every other
/// event, and bytes or an end for an exchange it holds no open record of, is ignored.
/// </summary>
/// <remarks>Not safe for concurrent use: <see cref="ExchangeRecordSubscriber"/> calls it under its lock.</remarks>
/// <param name="maxExchanges">The most records kept; positive.</param>
/// <param name="maxBytesPerExchange">The most bytes kept of one exchange; positive.</param>
internal sealed class ExchangeRecordBook(int maxExchanges, int maxBytesPerExchange)
{
    private readonly Queue<ExchangeRecorder> kept = new();
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
            case ExchangeBytesEvent bytes when open.TryGetValue(bytes.ExchangeId, out var recorder):
                recorder.Keep(ToSurlExchangeDirection(bytes.Direction), bytes.Bytes.Span);
                break;
            case ExchangeEndedEvent ended:
                open.Remove(ended.ExchangeId);
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

    private static SurlExchangeDirection ToSurlExchangeDirection(ExchangeDirection direction) =>
        direction == ExchangeDirection.Received ? SurlExchangeDirection.Received : SurlExchangeDirection.Sent;

    private void Open(ExchangeStartedEvent started)
    {
        if (kept.Count == maxExchanges)
        {
            open.Remove(kept.Dequeue().ExchangeId);
            DroppedExchangeCount++;
        }

        var recorder = new ExchangeRecorder(started, maxBytesPerExchange);
        kept.Enqueue(recorder);
        open[started.ExchangeId] = recorder;
    }

    private sealed class ExchangeRecorder(ExchangeStartedEvent started, int maxBytes)
    {
        private readonly List<SurlExchangeChunk> chunks = [];
        private readonly Uri listenUrl = new(started.ListenUrl);
        private int keptByteCount;
        private bool isTruncated;

        public long ExchangeId => started.ExchangeId;

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
            new(started.ExchangeId, listenUrl, started.Scheme, started.Client, started.At, [.. chunks], isTruncated);
    }
}
