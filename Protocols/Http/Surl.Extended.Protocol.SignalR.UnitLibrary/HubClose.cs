namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// A Close (type 7).
/// </summary>
/// <param name="Error">Why the connection ends, or <see langword="null"/> for a normal end.</param>
/// <param name="AllowReconnect">Whether the receiver may reconnect; the server never sends <see langword="true"/>.</param>
public sealed record HubClose(string? Error, bool AllowReconnect) : HubMessage
{
    /// <inheritdoc/>
    public override HubMessageType Type => HubMessageType.Close;
}
