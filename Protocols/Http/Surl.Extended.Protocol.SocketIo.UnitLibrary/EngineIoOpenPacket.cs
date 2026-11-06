using System.Buffers;
using System.Text.Json;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// Writes the open packet that starts every Engine.IO session: <c>0</c> then the handshake JSON object,
/// its members in EIO "Handshake"'s order with no whitespace (ADR-0120, decision 3).
/// </summary>
public static class EngineIoOpenPacket
{
    /// <summary>How often the server pings, in milliseconds: EIO "Handshake"'s example value.</summary>
    public const int PingIntervalMilliseconds = 25000;

    /// <summary>How long the server waits for a pong, in milliseconds: EIO "Handshake"'s example value.</summary>
    public const int PingTimeoutMilliseconds = 20000;

    /// <summary>Writes the open packet.</summary>
    /// <param name="sessionId">The Engine.IO session id, <c>sid</c>.</param>
    /// <param name="upgrades">The transports the session may upgrade to; empty on a WebSocket session.</param>
    /// <param name="pingIntervalMilliseconds">The <c>pingInterval</c>.</param>
    /// <param name="pingTimeoutMilliseconds">The <c>pingTimeout</c>.</param>
    /// <param name="maxPayload">The <c>maxPayload</c>: the most bytes a packet may hold, <c>--max-message</c>.</param>
    /// <returns>The open packet.</returns>
    public static EngineIoPacket Write(
        string sessionId,
        IReadOnlyList<string> upgrades,
        int pingIntervalMilliseconds,
        int pingTimeoutMilliseconds,
        long maxPayload)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(upgrades);

        var json = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(json))
        {
            writer.WriteStartObject();
            writer.WriteString("sid", sessionId);
            writer.WriteStartArray("upgrades");
            foreach (var upgrade in upgrades)
            {
                writer.WriteStringValue(upgrade);
            }

            writer.WriteEndArray();
            writer.WriteNumber("pingInterval", pingIntervalMilliseconds);
            writer.WriteNumber("pingTimeout", pingTimeoutMilliseconds);
            writer.WriteNumber("maxPayload", maxPayload);
            writer.WriteEndObject();
        }

        return new EngineIoPacket(EngineIoPacketType.Open, json.WrittenSpan.ToArray(), IsBinary: false);
    }
}
