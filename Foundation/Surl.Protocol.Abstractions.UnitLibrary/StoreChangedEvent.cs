namespace Surl.Protocol.Abstractions;

/// <summary>
/// One of the instance's shared stores changed.
/// </summary>
/// <param name="Store">Which store changed.</param>
/// <param name="Change">What changed.</param>
/// <param name="Name">
/// The path, mailbox, topic or queue, or account name the change touched.
/// </param>
/// <param name="ByteCount">How many bytes were written or delivered, or <see langword="null"/> when none apply.</param>
/// <param name="ExchangeId">The exchange that made the change, or <see langword="null"/> for a seed, reset or restore.</param>
public sealed record StoreChangedEvent(
    StoreKind Store,
    StoreChange Change,
    string Name,
    long? ByteCount,
    long? ExchangeId) : SurlEvent
{
    /// <summary>
    /// The name the entry had before a <see cref="StoreChange.Renamed"/> change;
    /// <see langword="null"/> for every other change.
    /// </summary>
    public string? PreviousName { get; init; }
}
