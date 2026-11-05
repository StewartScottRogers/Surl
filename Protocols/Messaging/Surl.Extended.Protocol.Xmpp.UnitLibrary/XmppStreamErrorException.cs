namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// Ends the stream with a stream error (RFC 6120 section 4.9): the server sends
/// <see cref="Condition"/>, then <c>&lt;/stream:stream&gt;</c>, then closes.
/// </summary>
/// <param name="condition">The condition sent, one of <see cref="XmppStreamErrors"/>.</param>
/// <param name="reason">Why, for the exchange log only; never sent to the client.</param>
internal sealed class XmppStreamErrorException(string condition, string reason) : Exception(reason)
{
    /// <summary>The condition sent, one of <see cref="XmppStreamErrors"/>.</summary>
    public string Condition { get; } = condition;
}
