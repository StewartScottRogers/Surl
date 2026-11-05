namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// A StreamItem (type 2).
/// </summary>
/// <param name="InvocationId">The stream's invocation or stream ID.</param>
/// <param name="Item">The item, as sent.</param>
public sealed record HubStreamItem(string InvocationId, HubJsonValue Item) : HubMessage
{
    /// <inheritdoc/>
    public override HubMessageType Type => HubMessageType.StreamItem;
}
