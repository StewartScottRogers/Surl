namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// What the hub answers one Invocation or StreamInvocation with (ADR-0119, decision 1): a
/// Completion with <paramref name="Result"/>, with <paramref name="Error"/> or with neither, a
/// stream of <paramref name="StreamItemCount"/> items before its Completion, or an upload read
/// from <see cref="UploadStreamId"/> before its Completion.
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
    /// Whether the call's arguments are first sent to every connection on the hub as a non-blocking
    /// Invocation of <c>Broadcast</c>.
    /// </summary>
    public bool Broadcasts { get; init; }

    /// <summary>
    /// For <c>Collect</c>, the stream ID whose client StreamItems are read until the client's
    /// Completion of it; <see langword="null"/> for a call that reads no upload.
    /// </summary>
    public string? UploadStreamId { get; init; }

    /// <summary>
    /// A call answered by a Completion carrying <paramref name="error"/>.
    /// </summary>
    /// <param name="error">The error text.</param>
    /// <returns>The call.</returns>
    public static SignalRHubCall Failed(string error) => new(null, error, null);

    /// <summary>
    /// <c>Broadcast</c>: the arguments sent to the hub, then a Completion with neither <c>result</c> nor <c>error</c>.
    /// </summary>
    /// <returns>The call.</returns>
    public static SignalRHubCall Broadcast() => new(null, null, null) { Broadcasts = true };

    /// <summary>
    /// <c>Collect</c>: the client's upload on <paramref name="streamId"/> read before the Completion.
    /// </summary>
    /// <param name="streamId">The upload's stream ID.</param>
    /// <returns>The call.</returns>
    public static SignalRHubCall Upload(string streamId) => new(null, null, null) { UploadStreamId = streamId };
}
