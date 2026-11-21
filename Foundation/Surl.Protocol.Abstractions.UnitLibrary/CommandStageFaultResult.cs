namespace Surl.Protocol.Abstractions;

/// <summary>
/// What <see cref="CommandStageFaults.FindAndApplyWithClientOutcomeAsync(ExchangeContext, IConnection, FaultPoint, string)"/>
/// found and how a delay hold ended (ADR-0141, amendment 3).
/// </summary>
/// <param name="Rule">The matching rule, or <see langword="null"/> when none matched.</param>
/// <param name="ClientLeft">
/// <see cref="PeerWatchOutcome.PeerClosed"/> or <see cref="PeerWatchOutcome.PeerReset"/> when a
/// <see cref="FaultKind.Delay"/> hold ended early because the client left; otherwise
/// <see cref="PeerWatchOutcome.NotWatched"/>.
/// </param>
public sealed record CommandStageFaultResult(FaultRule? Rule, PeerWatchOutcome ClientLeft);
