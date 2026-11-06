namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// How a session's WebSocket ends (ADR-0120, decisions 2, 3, 4 and 7): a WebSocket Close with
/// <paramref name="CloseCode"/>, or nothing at all when the client is gone or the exchange was
/// cancelled.
/// </summary>
/// <param name="CloseCode">The WebSocket Close's code, or <see langword="null"/> when nothing more is written.</param>
/// <param name="Why">Why, for the exchange log.</param>
internal sealed record SocketIoSessionEnd(ushort? CloseCode, string Why)
{
    public const ushort NormalClosure = 1000;

    public const ushort GoingAway = 1001;

    public const ushort ProtocolError = 1002;

    public const ushort InvalidPayload = 1007;

    public const ushort PolicyViolation = 1008;

    public const ushort MessageTooBig = 1009;

    /// <summary>
    /// A WebSocket Close with <paramref name="code"/>.
    /// </summary>
    public static SocketIoSessionEnd WebSocketClose(ushort code, string why) => new(code, why);

    /// <summary>
    /// The protocol error close, <c>1002</c>, with no Engine.IO packet first (EIO names none).
    /// </summary>
    public static SocketIoSessionEnd ProtocolErrorClose(string why) => new(ProtocolError, why);

    /// <summary>
    /// Nothing more written: the client is gone, or the exchange was cancelled.
    /// </summary>
    public static SocketIoSessionEnd Silent(string why) => new(null, why);
}
