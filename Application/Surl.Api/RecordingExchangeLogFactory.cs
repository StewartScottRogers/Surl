using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// Keeps what each exchange received and sent, for <see cref="SurlServer.Exchanges"/> (ADR-0099,
/// decision 7). At most <paramref name="maxExchanges"/> exchanges are kept, the oldest dropped and
/// counted in <see cref="DroppedExchangeCount"/> past it; at most
/// <paramref name="maxBytesPerExchange"/> bytes of each, both directions together, the rest cut and
/// the record marked truncated. Exchanges are recorded on the engine's tasks while a test reads, so
/// every member is safe to call from any thread. Notes are not recorded.
/// </summary>
/// <param name="maxExchanges">The most exchanges kept; positive.</param>
/// <param name="maxBytesPerExchange">The most bytes kept of one exchange; positive.</param>
/// <param name="timeProvider">The clock that stamps when each exchange started.</param>
internal sealed class RecordingExchangeLogFactory(int maxExchanges, int maxBytesPerExchange, TimeProvider timeProvider)
    : IExchangeLogFactory
{
    private readonly Queue<ExchangeRecorder> recorders = new();
    private long droppedExchangeCount;

    /// <summary>How many exchanges were dropped, oldest first, to keep at most the cap.</summary>
    public long DroppedExchangeCount
    {
        get
        {
            lock (recorders)
            {
                return droppedExchangeCount;
            }
        }
    }

    /// <inheritdoc/>
    public IExchangeLog Create(long exchangeId, EndPoint remoteEndPoint)
    {
        var recorder = new ExchangeRecorder(exchangeId, remoteEndPoint, timeProvider.GetUtcNow(), maxBytesPerExchange);
        lock (recorders)
        {
            if (recorders.Count == maxExchanges)
            {
                recorders.Dequeue();
                droppedExchangeCount++;
            }

            recorders.Enqueue(recorder);
        }

        return recorder;
    }

    /// <summary>Records nothing: a note outside any exchange is a log line, which the options' log carries.</summary>
    /// <param name="text">The note, ignored.</param>
    public void NoteOutsideExchange(string text)
    {
    }

    /// <summary>The exchanges kept, oldest first, as they stand now.</summary>
    /// <returns>A snapshot; bytes recorded later do not change it.</returns>
    public IReadOnlyList<SurlExchangeRecord> Snapshot()
    {
        ExchangeRecorder[] kept;
        lock (recorders)
        {
            kept = [.. recorders];
        }

        return [.. kept.Select(recorder => recorder.Snapshot())];
    }

    private sealed class ExchangeRecorder(long exchangeId, EndPoint client, DateTimeOffset startedAt, int maxBytes) : IExchangeLog
    {
        private readonly List<SurlExchangeChunk> chunks = [];
        private int keptByteCount;
        private bool isTruncated;

        public void BytesReceived(ReadOnlySpan<byte> bytes) => Keep(SurlExchangeDirection.Received, bytes);

        public void BytesSent(ReadOnlySpan<byte> bytes) => Keep(SurlExchangeDirection.Sent, bytes);

        public void Note(string text)
        {
        }

        public SurlExchangeRecord Snapshot()
        {
            lock (chunks)
            {
                return new SurlExchangeRecord(exchangeId, client, startedAt, [.. chunks], isTruncated);
            }
        }

        private void Keep(SurlExchangeDirection direction, ReadOnlySpan<byte> bytes)
        {
            lock (chunks)
            {
                var keptLength = Math.Min(bytes.Length, maxBytes - keptByteCount);
                isTruncated |= keptLength < bytes.Length;
                if (keptLength > 0)
                {
                    chunks.Add(new SurlExchangeChunk(direction, bytes[..keptLength].ToArray()));
                    keptByteCount += keptLength;
                }
            }
        }
    }
}
