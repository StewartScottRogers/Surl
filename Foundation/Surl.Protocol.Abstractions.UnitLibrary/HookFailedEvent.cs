namespace Surl.Protocol.Abstractions;

/// <summary>
/// A <c>Surl.Api</c> hook threw or ran past its time limit, and the point was served as if no hook
/// were registered (ADR-0124, decision 5).
/// </summary>
/// <param name="HookId">The hook's identifier.</param>
/// <param name="Failure">How it failed.</param>
/// <param name="Message">The exception's message, or what timed out.</param>
public sealed record HookFailedEvent(string HookId, HookFailure Failure, string Message) : SurlEvent;
