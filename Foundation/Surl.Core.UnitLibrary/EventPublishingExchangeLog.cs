using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// Passes an exchange's bytes and notes to its log unchanged, counts the bytes each way, and
/// publishes each chunk as an <see cref="ExchangeBytesEvent"/>, copied, while the publisher wants
/// bytes (ADR-0124, decision 1). The engine hands it to the wrappers that report bytes, so every
/// byte the log sees, data connections included, reaches the event stream in the same order.
/// </summary>
/// <param name="log">The exchange's log.</param>
/// <param name="events">The instance's event stream.</param>
/// <param name="exchangeId">The exchange the bytes belong to.</param>
internal sealed class EventPublishingExchangeLog(IExchangeLog log, ISurlEventPublisher events, long exchangeId) : IExchangeLog
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

    /// <inheritdoc/>
    public void BytesReceived(ReadOnlySpan<byte> bytes)
    {
        Interlocked.Add(ref receivedByteCount, bytes.Length);
        PublishBytes(ExchangeDirection.Received, bytes);
        log.BytesReceived(bytes);
    }

    /// <inheritdoc/>
    public void BytesSent(ReadOnlySpan<byte> bytes)
    {
        Interlocked.Add(ref sentByteCount, bytes.Length);
        PublishBytes(ExchangeDirection.Sent, bytes);
        log.BytesSent(bytes);
    }

    /// <inheritdoc/>
    public void Note(string text) => log.Note(text);

    private void PublishBytes(ExchangeDirection direction, ReadOnlySpan<byte> bytes)
    {
        if (events.IsPublishingBytes)
        {
            events.Publish(new ExchangeBytesEvent(exchangeId, direction, bytes.ToArray()));
        }
    }
}
