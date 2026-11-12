namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// What a transport hands the session when it is asked for the client's next bytes: a payload, or the
/// end of the connection (ADR-0119, decision 2, amendment 1).
/// </summary>
/// <param name="Payload">The bytes the client sent in one WebSocket message or one HTTP Post body, or <see langword="null"/> when the connection ended.</param>
/// <param name="End">How the connection ended; <see langword="null"/> when there is a payload.</param>
internal sealed record SignalRTransportRead(byte[]? Payload, SignalRSessionEnd? End)
{
    /// <summary>
    /// A read that found client bytes.
    /// </summary>
    public static SignalRTransportRead OfPayload(byte[] payload) => new(payload, null);

    /// <summary>
    /// A read that found the connection's end.
    /// </summary>
    public static SignalRTransportRead Ended(SignalRSessionEnd end) => new(null, end);
}
