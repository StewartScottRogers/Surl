namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// An Invocation (type 1), or a StreamInvocation (type 4) when <paramref name="IsStreaming"/>.
/// </summary>
/// <param name="IsStreaming">Whether it is a StreamInvocation.</param>
/// <param name="InvocationId">
/// The invocation's ID, or <see langword="null"/> for a non-blocking Invocation; a StreamInvocation
/// always has one.
/// </param>
/// <param name="Target">The method called, compared case-sensitively.</param>
/// <param name="Arguments">The arguments, each as sent.</param>
/// <param name="StreamIds">The IDs of the streams the caller uploads to the method; empty when none.</param>
public sealed record HubInvocation(
    bool IsStreaming,
    string? InvocationId,
    string Target,
    IReadOnlyList<HubJsonValue> Arguments,
    IReadOnlyList<string> StreamIds) : HubMessage
{
    /// <inheritdoc/>
    public override HubMessageType Type => IsStreaming ? HubMessageType.StreamInvocation : HubMessageType.Invocation;
}
