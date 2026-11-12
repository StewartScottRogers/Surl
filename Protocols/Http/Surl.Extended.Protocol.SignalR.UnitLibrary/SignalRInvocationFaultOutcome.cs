namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// What the fault plan decided for one invocation (<see cref="SignalRInvocationFaults.ApplyAsync"/>):
/// call the hub method as usual, complete with a chosen error, or end the connection unanswered.
/// </summary>
/// <param name="ChosenError">The chosen Completion <c>error</c> text, or <see langword="null"/> for none.</param>
/// <param name="IsDropped">Whether a <c>drop</c> rule ended the connection in place of any answer.</param>
internal readonly record struct SignalRInvocationFaultOutcome(string? ChosenError, bool IsDropped)
{
    /// <summary>The hub method is called as usual.</summary>
    public static SignalRInvocationFaultOutcome CalledAsUsual { get; } = new(null, false);

    /// <summary>The connection ends and nothing more is written.</summary>
    public static SignalRInvocationFaultOutcome Dropped { get; } = new(null, true);
}
