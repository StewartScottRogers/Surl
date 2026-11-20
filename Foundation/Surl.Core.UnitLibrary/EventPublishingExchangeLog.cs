using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// Passes an exchange's bytes and notes to its log unchanged, counts the bytes each way, and
/// publishes each chunk as an <see cref="ExchangeBytesEvent"/>, copied, while the publisher wants
/// bytes (ADR-0124, decision 1). The engine hands it to the wrappers that report bytes, so every
/// byte the log sees, data connections included, reaches the event stream in the same order.
/// When the server redacts (ADR-0182), <paramref name="log"/> is the redacting log for the control
/// bytes, and <see cref="PayloadLog"/> reaches the data connections, whose bytes are the client's
/// payload and not credentials (ADR-0182, decision 8), through <paramref name="payloadLog"/>.
/// </summary>
/// <param name="log">The log the control connection's bytes and notes go to.</param>
/// <param name="events">The instance's event stream.</param>
/// <param name="exchangeId">The exchange the bytes belong to.</param>
/// <param name="payloadLog">The log data connections' bytes go to: the level's unredacted log.</param>
internal sealed class EventPublishingExchangeLog(
    IExchangeLog log, ISurlEventPublisher events, long exchangeId, IExchangeLog payloadLog) : IExchangeLog
{
    private long receivedByteCount;
    private long sentByteCount;

    /// <summary>
    /// How many bytes the server has received so far.
    /// </summary>
    public long ReceivedByteCount => Interlocked.Read(ref receivedByteCount);

    /// <summary>
    /// How many bytes the server has sent so far.
    /// </summary>
    public long SentByteCount => Interlocked.Read(ref sentByteCount);

    /// <summary>
    /// The log for data connections: counted and published like this one, written unredacted.
    /// </summary>
    public IExchangeLog PayloadLog => new PayloadView(this, payloadLog);

    /// <inheritdoc/>
    public void BytesReceived(ReadOnlySpan<byte> bytes)
    {
        CountAndPublishReceived(bytes);
        log.BytesReceived(bytes);
    }

    /// <inheritdoc/>
    public void BytesSent(ReadOnlySpan<byte> bytes)
    {
        CountAndPublishSent(bytes);
        log.BytesSent(bytes);
    }

    /// <inheritdoc/>
    public void Note(string text) => log.Note(text);

    private void CountAndPublishReceived(ReadOnlySpan<byte> bytes)
    {
        Interlocked.Add(ref receivedByteCount, bytes.Length);
        PublishBytes(ExchangeDirection.Received, bytes);
    }

    private void CountAndPublishSent(ReadOnlySpan<byte> bytes)
    {
        Interlocked.Add(ref sentByteCount, bytes.Length);
        PublishBytes(ExchangeDirection.Sent, bytes);
    }

    private void PublishBytes(ExchangeDirection direction, ReadOnlySpan<byte> bytes)
    {
        if (events.IsPublishingBytes)
        {
            events.Publish(new ExchangeBytesEvent(exchangeId, direction, bytes.ToArray()));
        }
    }

    private sealed class PayloadView(EventPublishingExchangeLog owner, IExchangeLog target) : IExchangeLog
    {
        public void BytesReceived(ReadOnlySpan<byte> bytes)
        {
            owner.CountAndPublishReceived(bytes);
            target.BytesReceived(bytes);
        }

        public void BytesSent(ReadOnlySpan<byte> bytes)
        {
            owner.CountAndPublishSent(bytes);
            target.BytesSent(bytes);
        }

        public void Note(string text) => target.Note(text);
    }
}
