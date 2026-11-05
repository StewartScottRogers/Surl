namespace Surl.Protocol.Abstractions;

/// <summary>
/// The default <see cref="ExchangeContext.Faults"/>: a plan with no rules, which finds no fault at
/// any point, so a server given no plan never misbehaves.
/// </summary>
public sealed class EmptyFaultPlan : IFaultPlan
{
    private EmptyFaultPlan()
    {
    }

    /// <summary>
    /// The one instance.
    /// </summary>
    public static EmptyFaultPlan Instance { get; } = new();

    /// <inheritdoc/>
    /// <remarks>Always <see langword="null"/>, synchronously.</remarks>
    public ValueTask<FaultRule?> FindAsync(FaultPoint point, CancellationToken cancellationToken) =>
        ValueTask.FromResult<FaultRule?>(null);
}
