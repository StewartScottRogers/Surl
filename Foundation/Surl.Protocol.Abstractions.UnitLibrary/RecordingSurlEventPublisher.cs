namespace Surl.Protocol.Abstractions;

/// <summary>
/// An <see cref="ISurlEventPublisher"/> that keeps every event published, in order, so a protocol,
/// store or login test can set <see cref="ExchangeContext.Events"/> and assert what was published
/// without an event stream. It stamps nothing: each event keeps the <see cref="SurlEvent.Sequence"/>
/// and <see cref="SurlEvent.At"/> it was published with. Safe under concurrent publishes.
/// </summary>
/// <param name="isPublishingBytes">
/// What <see cref="IsPublishingBytes"/> reports, so a test can show a server publishes bytes only when asked.
/// </param>
public sealed class RecordingSurlEventPublisher(bool isPublishingBytes = true) : ISurlEventPublisher
{
    private readonly List<SurlEvent> events = [];
    private readonly Lock eventsLock = new();

    /// <inheritdoc/>
    public bool IsPublishingBytes => isPublishingBytes;

    /// <summary>
    /// A snapshot of every event published so far, in order.
    /// </summary>
    public IReadOnlyList<SurlEvent> Events
    {
        get
        {
            lock (eventsLock)
            {
                return [.. events];
            }
        }
    }

    /// <inheritdoc/>
    public void Publish(SurlEvent surlEvent)
    {
        ArgumentNullException.ThrowIfNull(surlEvent);

        lock (eventsLock)
        {
            events.Add(surlEvent);
        }
    }
}
