namespace Surl.Protocol.Abstractions;

/// <summary>
/// Where a server, store or login check publishes what happened on its instance's one event stream
/// (ADR-0124, decision 1). It reaches a server through <see cref="ExchangeContext.Events"/>.
/// </summary>
public interface ISurlEventPublisher
{
    /// <summary>
    /// Whether at least one subscription asked for bytes. A publisher copies a chunk into an
    /// <see cref="ExchangeBytesEvent"/> only while this is <see langword="true"/>.
    /// </summary>
    bool IsPublishingBytes { get; }

    /// <summary>
    /// Publishes one event. Never blocks and never throws into the caller. The caller constructs the
    /// event with <see cref="SurlEvent.Sequence"/> 0 and <see cref="SurlEvent.At"/> default; the stream
    /// stamps both.
    /// </summary>
    /// <param name="surlEvent">The event to publish.</param>
    void Publish(SurlEvent surlEvent);
}
