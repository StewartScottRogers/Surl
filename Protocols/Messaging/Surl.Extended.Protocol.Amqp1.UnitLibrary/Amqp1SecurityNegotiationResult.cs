namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// How the protocol-header and SASL negotiation ended.
/// </summary>
/// <param name="IsReadyForAmqp">
/// <see langword="true"/> when both sides have exchanged <c>AMQP 0 1 0 0</c> and AMQP frames
/// follow; <see langword="false"/> when the connection was closed.
/// </param>
/// <param name="AccountName">
/// The account SASL logged in; <see langword="null"/> when the connection is anonymous or closed.
/// </param>
public sealed record Amqp1SecurityNegotiationResult(bool IsReadyForAmqp, string? AccountName)
{
    /// <summary>The negotiation closed the connection.</summary>
    public static Amqp1SecurityNegotiationResult Closed { get; } = new(false, null);
}
