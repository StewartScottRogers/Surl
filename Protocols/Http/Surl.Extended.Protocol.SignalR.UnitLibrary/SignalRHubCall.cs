namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// What the hub answers one Invocation or StreamInvocation with (ADR-0119, decision 1): a
/// Completion with <paramref name="Result"/>, with <paramref name="Error"/> or with neither, or a
/// stream of <paramref name="StreamItemCount"/> items before its Completion.
/// </summary>
/// <param name="Result">The Completion's <c>result</c>, or <see langword="null"/> when it has none.</param>
/// <param name="Error">The Completion's <c>error</c>, or <see langword="null"/> when it has none.</param>
/// <param name="StreamItemCount">
/// For <c>Count</c>, how many StreamItems to send, <c>1</c> to <c>n</c>, before a Completion with no
/// <c>result</c>; <see langword="null"/> for a call answered by its Completion alone.
/// </param>
public sealed record SignalRHubCall(HubJsonValue? Result, string? Error, int? StreamItemCount)
{
    /// <summary>
    /// A call answered by a Completion carrying <paramref name="error"/>.
    /// </summary>
    /// <param name="error">The error text.</param>
    /// <returns>The call.</returns>
    public static SignalRHubCall Failed(string error) => new(null, error, null);
}
