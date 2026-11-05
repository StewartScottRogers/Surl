namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// A Completion (type 3): with a <paramref name="Result"/>, with an <paramref name="Error"/>, or with
/// neither; never both.
/// </summary>
/// <param name="InvocationId">The invocation or stream it completes.</param>
/// <param name="Result">The result, or <see langword="null"/> when it has none.</param>
/// <param name="Error">The error text, or <see langword="null"/> when it has none.</param>
public sealed record HubCompletion(string InvocationId, HubJsonValue? Result, string? Error) : HubMessage
{
    /// <inheritdoc/>
    public override HubMessageType Type => HubMessageType.Completion;
}
