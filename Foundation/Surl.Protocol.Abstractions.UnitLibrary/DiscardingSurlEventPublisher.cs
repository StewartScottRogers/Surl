namespace Surl.Protocol.Abstractions;

/// <summary>
/// The default <see cref="ExchangeContext.Events"/>: publishes nothing and never asks for bytes, so a
/// server given no event stream behaves as it did before the stream existed.
/// </summary>
public sealed class DiscardingSurlEventPublisher : ISurlEventPublisher
{
    private DiscardingSurlEventPublisher()
    {
    }

    /// <summary>
    /// The one instance.
    /// </summary>
    public static DiscardingSurlEventPublisher Instance { get; } = new();

    /// <inheritdoc/>
    /// <remarks>Always <see langword="false"/>.</remarks>
    public bool IsPublishingBytes => false;

    /// <inheritdoc/>
    /// <remarks>Discards the event.</remarks>
    public void Publish(SurlEvent surlEvent)
    {
    }
}
