using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// Decides, at one point its <see cref="FaultOverrideScope"/> matches, which fault rule applies there
/// (ADR-0124, decision 5): a <c>Surl.Api</c> response override, bounded by its caller.
/// </summary>
/// <param name="overrideId">The override's identifier in the plan, <c>f&lt;n&gt;</c>.</param>
/// <param name="point">What is known at the point.</param>
/// <param name="cancellationToken">Cancels the decision.</param>
/// <returns>The rule to apply at the point, whose stage must be the point's, or <see langword="null"/> to go on to the plan's next rule.</returns>
public delegate ValueTask<FaultRule?> FaultOverride(string overrideId, FaultPoint point, CancellationToken cancellationToken);
