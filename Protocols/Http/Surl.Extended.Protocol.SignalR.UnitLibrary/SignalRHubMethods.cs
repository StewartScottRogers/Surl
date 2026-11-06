using System.Text.Json;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The hub's methods, by <c>target</c> compared case-sensitively (HubProtocol.md "Invocations",
/// "Streaming" and "Completion and results"; ADR-0119, decision 1): <c>Echo</c> completes with its
/// one argument byte for byte, <c>Count</c> streams <c>1</c> to <c>n</c>, and <c>Fail</c> completes
/// with an error. Every error text is fixed and never repeats the client's bytes (ADR-0006).
/// </summary>
/// <remarks>
/// <c>Broadcast</c> and <c>Collect</c> are not served yet, so they are answered as unknown methods.
/// </remarks>
public static class SignalRHubMethods
{
    /// <summary>
    /// The most items <c>Count</c> streams (ADR-0119, decision 7).
    /// </summary>
    public const int MaxCount = 1000;

    /// <summary>
    /// The <c>error</c> for a <c>target</c> the hub has no method for.
    /// </summary>
    public const string UnknownMethodText = "Unknown hub method.";

    /// <summary>
    /// The <c>error</c> for a streaming method invoked as an Invocation, or the reverse.
    /// </summary>
    public const string WrongMessageTypeText = "The method was invoked with the wrong message type.";

    /// <summary>
    /// The <c>error</c> for the wrong arguments, or for <c>streamIds</c> on a method that reads none.
    /// </summary>
    public const string InvalidArgumentsText = "Invalid arguments for the hub method.";

    /// <summary>
    /// The <c>error</c> <c>Fail</c> always completes with.
    /// </summary>
    public const string FailText = "An unexpected error occurred invoking 'Fail' on the server.";

    /// <summary>
    /// Decides what <paramref name="invocation"/> is answered with.
    /// </summary>
    /// <param name="invocation">The Invocation or StreamInvocation.</param>
    /// <returns>Its answer.</returns>
    public static SignalRHubCall Call(HubInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        return invocation.Target switch
        {
            "Echo" => Echo(invocation),
            "Count" => Count(invocation),
            "Fail" => SignalRHubCall.Failed(FailText),
            _ => SignalRHubCall.Failed(UnknownMethodText),
        };
    }

    private static SignalRHubCall Echo(HubInvocation invocation)
    {
        if (invocation.IsStreaming)
        {
            return SignalRHubCall.Failed(WrongMessageTypeText);
        }

        return HasOneArgumentAndNoStreams(invocation)
            ? new SignalRHubCall(invocation.Arguments[0], null, null)
            : SignalRHubCall.Failed(InvalidArgumentsText);
    }

    private static SignalRHubCall Count(HubInvocation invocation)
    {
        if (!invocation.IsStreaming)
        {
            return SignalRHubCall.Failed(WrongMessageTypeText);
        }

        return HasOneArgumentAndNoStreams(invocation) && TryReadCount(invocation.Arguments[0], out var count)
            ? new SignalRHubCall(null, null, count)
            : SignalRHubCall.Failed(InvalidArgumentsText);
    }

    private static bool HasOneArgumentAndNoStreams(HubInvocation invocation) =>
        invocation.Arguments.Count == 1 && invocation.StreamIds.Count == 0;

    // An integer from 0 to MaxCount; the reader has already found the value well-formed JSON.
    private static bool TryReadCount(HubJsonValue argument, out int count)
    {
        var reader = new Utf8JsonReader(argument.Utf8Json.Span);
        reader.Read();
        count = 0;

        return reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out count) && count is >= 0 and <= MaxCount;
    }
}
