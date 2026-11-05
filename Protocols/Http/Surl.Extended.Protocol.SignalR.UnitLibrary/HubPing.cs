namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// A Ping (type 6).
/// </summary>
public sealed record HubPing : HubMessage
{
    /// <inheritdoc/>
    public override HubMessageType Type => HubMessageType.Ping;
}
