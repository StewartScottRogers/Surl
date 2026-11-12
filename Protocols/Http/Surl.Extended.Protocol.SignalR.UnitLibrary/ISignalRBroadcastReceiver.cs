namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// A hub connection that has finished its handshake and so receives <c>Broadcast</c>'s Invocations
/// (ADR-0119, decisions 1 and 7).
/// </summary>
internal interface ISignalRBroadcastReceiver
{
    /// <summary>
    /// Queues <paramref name="invocation"/>, written in the connection's encoding, for the connection without waiting for it to be written, or,
    /// when it would put the connection past its unsent-bytes cap, queues nothing and closes it.
    /// </summary>
    /// <param name="invocation">The broadcast Invocation.</param>
    void ReceiveBroadcast(HubInvocation invocation);
}
