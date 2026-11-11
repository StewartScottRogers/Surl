namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// How a hub connection's WebSocket ends (ADR-0119, decisions 3, 4 and 7): the hub Close record
/// sent first, if any, then a WebSocket Close with <paramref name="CloseCode"/>, or nothing at all
/// when the client is gone.
/// </summary>
/// <param name="HubCloseMessage">The Close (type 7) message, written in the connection's encoding, sent before the WebSocket Close, or <see langword="null"/>.</param>
/// <param name="CloseCode">The WebSocket Close's code, or <see langword="null"/> when nothing more is written.</param>
/// <param name="Why">Why, for the exchange log.</param>
internal sealed record SignalRSessionEnd(HubClose? HubCloseMessage, ushort? CloseCode, string Why)
{
    /// <summary>
    /// The text of the Close sent for a protocol error (HubProtocol.md "Errors").
    /// </summary>
    public const string ProtocolErrorText = "Connection closed with an error.";

    /// <summary>
    /// The text of the Close sent to a client silent past the client timeout (HubProtocol.md "Ping").
    /// </summary>
    public const string ClientTimeoutText = "Server timeout elapsed without receiving a message from the client.";

    public const ushort NormalClosure = 1000;

    public const ushort GoingAway = 1001;

    public const ushort ProtocolError = 1002;

    public const ushort InvalidPayload = 1007;

    public const ushort PolicyViolation = 1008;

    public const ushort MessageTooBig = 1009;

    /// <summary>
    /// A WebSocket Close with <paramref name="code"/> and no hub Close before it.
    /// </summary>
    public static SignalRSessionEnd WebSocketClose(ushort code, string why) => new(null, code, why);

    /// <summary>
    /// A hub Close carrying <paramref name="error"/>, then a WebSocket Close with <paramref name="code"/>.
    /// </summary>
    public static SignalRSessionEnd HubClose(string error, ushort code, string why) =>
        new(new HubClose(error, AllowReconnect: false), code, why);

    /// <summary>
    /// The hub protocol error close: <see cref="ProtocolErrorText"/>, then <c>1002</c>.
    /// </summary>
    public static SignalRSessionEnd HubProtocolError(string why) => HubClose(ProtocolErrorText, ProtocolError, why);

    /// <summary>
    /// Whether this is the farewell to a cancelled exchange, written on its own deadline because the
    /// exchange's cancellation token is already cancelled (ADR-0119 decision 7 and its BL-772 amendment).
    /// </summary>
    public bool IsFarewell { get; private init; }

    /// <summary>
    /// Nothing more written: the client is gone.
    /// </summary>
    public static SignalRSessionEnd Silent(string why) => new(null, null, why);

    /// <summary>
    /// The farewell at shutdown or at an exchange limit: a Close <c>{"type":7}</c>, then WebSocket Close <c>1001</c>.
    /// </summary>
    public static SignalRSessionEnd Farewell(string why) =>
        new(new HubClose(null, AllowReconnect: false), GoingAway, why) { IsFarewell = true };
}
