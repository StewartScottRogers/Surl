namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// A CancelInvocation (type 5).
/// </summary>
/// <param name="InvocationId">The stream to stop.</param>
public sealed record HubCancelInvocation(string InvocationId) : HubMessage
{
    /// <inheritdoc/>
    public override HubMessageType Type => HubMessageType.CancelInvocation;
}
