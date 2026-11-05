namespace Surl.MessageBus;

/// <summary>
/// What <see cref="BusConnection.Subscribe"/> did.
/// </summary>
/// <param name="Outcome">Whether the subscription was made, or which refusal it met.</param>
/// <param name="Subscription">The live subscription; <see langword="null"/> when refused.
/// Dispose it to stop receiving.</param>
/// <param name="RetainedMessages">The retained messages the filter matches, in ordinal order of
/// topic, when the subscribe asked for them; otherwise, and when refused, empty. Taken at the
/// moment the subscription began, so every later publish reaches it live instead.</param>
public sealed record BusSubscribeResult(
    MessageBusOutcome Outcome,
    BusSubscription? Subscription,
    IReadOnlyList<BusMessage> RetainedMessages);
