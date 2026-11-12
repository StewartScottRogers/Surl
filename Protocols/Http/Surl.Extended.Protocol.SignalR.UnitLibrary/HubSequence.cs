namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// A Sequence (type 9): the first message after a reconnect's handshake, naming the sequence ID of
/// the first numbered message that follows (HubProtocol.md "Sequence Message Encoding"; ADR-0119,
/// decision 6).
/// </summary>
/// <param name="SequenceId">The sequence ID of the next numbered message the sender sends.</param>
public sealed record HubSequence(int SequenceId) : HubMessage
{
    /// <inheritdoc/>
    public override HubMessageType Type => HubMessageType.Sequence;
}
