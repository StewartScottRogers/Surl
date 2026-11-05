namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// One hub-protocol message, as HubProtocol.md "JSON Encoding" gives its members (ADR-0119,
/// decision 3). Message headers are read and ignored, so no message carries them.
/// </summary>
public abstract record HubMessage
{
    private protected HubMessage()
    {
    }

    /// <summary>
    /// The message's type.
    /// </summary>
    public abstract HubMessageType Type { get; }
}
