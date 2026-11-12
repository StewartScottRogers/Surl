namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// An Ack (type 8): tells the peer how many of its numbered messages have been received, so it may
/// stop keeping them (HubProtocol.md "Ack Message Encoding"; ADR-0119, decision 6).
/// </summary>
/// <param name="SequenceId">The sequence ID of the last message received.</param>
public sealed record HubAck(int SequenceId) : HubMessage
{
    /// <inheritdoc/>
    public override HubMessageType Type => HubMessageType.Ack;
}
