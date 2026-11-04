using System.Security.Cryptography;
using System.Text;

namespace Surl.Protocol.Ws;

/// <summary>
/// Computes the <c>Sec-WebSocket-Accept</c> value a server answers a client's
/// <c>Sec-WebSocket-Key</c> with (RFC 6455 section 4.2.2, step 5).
/// </summary>
internal static class WebSocketAcceptKey
{
    /// <summary>
    /// The GUID section 1.3 appends to the client's key before hashing.
    /// </summary>
    public const string KeyGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    /// <summary>
    /// Returns the base64 of the SHA-1 of <paramref name="secWebSocketKey"/> followed by <see cref="KeyGuid"/>.
    /// </summary>
    /// <param name="secWebSocketKey">The <c>Sec-WebSocket-Key</c> header's value, with surrounding whitespace already trimmed.</param>
    /// <returns>The <c>Sec-WebSocket-Accept</c> header's value.</returns>
    public static string Compute(string secWebSocketKey) =>
        Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(secWebSocketKey + KeyGuid)));
}
