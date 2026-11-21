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
    /// <inheritdoc />
    public override long? ExchangeId { get; } = ExchangeId;

    /// <summary>
    /// The account the change is about: <see cref="Name"/> when the store is <see cref="StoreKind.Accounts"/>,
    /// otherwise <see langword="null"/>.
    /// </summary>
    public override string? Account => Store == StoreKind.Accounts ? Name : null;

    /// <inheritdoc />
    public override void WriteMembers(ISurlEventMemberWriter writer)
    {
        writer.Choice(nameof(Store), Store);
        writer.Choice(nameof(Change), Change);
        writer.Text(nameof(Name), Name);
        writer.Number(nameof(ByteCount), ByteCount);
        writer.Number(nameof(ExchangeId), ExchangeId);
        writer.Text(nameof(PreviousName), PreviousName);
        writer.TextList(nameof(DeliveredFor), DeliveredFor);
    }

    /// <summary>
    /// The name the entry had before a <see cref="StoreChange.Renamed"/> change;
    /// <see langword="null"/> for every other change.
    /// </summary>
    public string? PreviousName { get; init; }

    /// <summary>
    /// The envelope addresses, as sent and in <c>RCPT</c> order, that resolved to the owner of this mail copy
    /// (ADR-0053 Amendment 1, decision 7); <see langword="null"/> when none apply, as for every store but mail.
    /// </summary>
    public IReadOnlyList<string>? DeliveredFor { get; init; }
}
