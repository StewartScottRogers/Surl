namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// How the server judged a handshake request (ADR-0119, decision 3).
/// </summary>
public enum HubHandshakeOutcome
{
    /// <summary>
    /// <c>{"protocol":"json","version":1}</c>: answered <c>{}</c>.
    /// </summary>
    Accepted,

    /// <summary>
    /// <c>{"protocol":"messagepack","version":1}</c>: answered <c>{}</c>, then every message is binary
    /// (BL-756).
    /// </summary>
    AcceptedMessagePack,

    /// <summary>
    /// A protocol other than <c>json</c> or <c>messagepack</c>, or a version other than 1: answered
    /// <see cref="HubHandshake.ProtocolNotAvailableText"/>.
    /// </summary>
    ProtocolNotAvailable,

    /// <summary>
    /// Not JSON, not an object, or without <c>protocol</c> or <c>version</c> of their kind:
    /// answered <see cref="HubHandshake.InvalidRequestText"/>.
    /// </summary>
    Invalid,
}
