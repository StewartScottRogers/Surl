namespace Surl.Protocol.Abstractions;

/// <summary>
/// The rules that make an instance misbehave on purpose (ADR-0124, decision 4). A server, store or
/// login check asks it at each <see cref="FaultPoint"/>; it reaches a server through
/// <see cref="ExchangeContext.Faults"/>.
/// </summary>
public interface IFaultPlan
{
    /// <summary>
    /// Finds the rule to apply at a point, or <see langword="null"/> for none. Asynchronous because a
    /// <c>Surl.Api</c> hook may decide; it completes synchronously when no hook is registered.
    /// </summary>
    /// <param name="point">What is known at the point.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The rule to apply, or <see langword="null"/>.</returns>
    ValueTask<FaultRule?> FindAsync(FaultPoint point, CancellationToken cancellationToken);
}
