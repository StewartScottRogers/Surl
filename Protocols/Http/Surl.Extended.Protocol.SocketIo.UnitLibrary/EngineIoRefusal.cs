using System.Text;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// The coded <c>400 Bad Request</c> bodies an Engine.IO request is refused with (ADR-0120,
/// decision 1), each a fixed ASCII JSON object naming the code and its text.
/// </summary>
internal static class EngineIoRefusal
{
    /// <summary>Code 0: <c>transport</c> absent or not served.</summary>
    public const int TransportUnknown = 0;

    /// <summary>Code 1: a <c>sid</c> no live session has.</summary>
    public const int SessionIdUnknown = 1;

    /// <summary>Code 2: a request without <c>sid</c> whose method is not <c>GET</c>.</summary>
    public const int BadHandshakeMethod = 2;

    /// <summary>Code 3: a second WebSocket for a session, a second concurrent poll or post, a polling request for a session on a WebSocket, a malformed post, or a WebSocket request that is not an upgrade.</summary>
    public const int BadRequest = 3;

    /// <summary>Code 5: <c>EIO</c> absent or not <c>4</c>.</summary>
    public const int UnsupportedProtocolVersion = 5;

    /// <summary>
    /// The body for <paramref name="code"/>, e.g. <c>{"code":0,"message":"Transport unknown"}</c>.
    /// </summary>
    public static byte[] Body(int code) => Encoding.ASCII.GetBytes(code switch
    {
        TransportUnknown => "{\"code\":0,\"message\":\"Transport unknown\"}",
        SessionIdUnknown => "{\"code\":1,\"message\":\"Session ID unknown\"}",
        BadHandshakeMethod => "{\"code\":2,\"message\":\"Bad handshake method\"}",
        BadRequest => "{\"code\":3,\"message\":\"Bad request\"}",
        _ => "{\"code\":5,\"message\":\"Unsupported protocol version\"}",
    });
}
