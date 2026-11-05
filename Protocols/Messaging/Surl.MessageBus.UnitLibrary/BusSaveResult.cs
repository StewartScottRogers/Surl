namespace Surl.MessageBus;

/// <summary>
/// What <see cref="MessageBus.SaveChangesAsync"/> or <see cref="MessageBus.LoadAsync"/> did with
/// the bus's files (ADR-0100, decision 3.7).
/// </summary>
/// <param name="Outcome"><see cref="MessageBusOutcome.Succeeded"/> when the files hold the bus's
/// state, or <see cref="MessageBusOutcome.StorageFailed"/> when a write failed: the change stays
/// in memory, the next save writes it, and the server notes <paramref name="FailureMessage"/>
/// with <c>IExchangeLog.Note</c>; serving goes on.</param>
/// <param name="FailureMessage">The failed write's exception message; <see langword="null"/>
/// when nothing failed.</param>
public sealed record BusSaveResult(MessageBusOutcome Outcome, string? FailureMessage)
{
    /// <summary>The result of a save that wrote everything it had to, or had nothing to write.</summary>
    public static BusSaveResult Saved { get; } = new(MessageBusOutcome.Succeeded, null);
}
