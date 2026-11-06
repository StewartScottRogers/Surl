using System.Text.Json;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// Judges a Socket.IO packet's JSON payload against its type (SIO "Packet encoding", "Sending and
/// receiving data", "Acknowledgement", "History"; ADR-0120, decisions 4 to 7).
/// </summary>
/// <remarks>
/// The payload must be one JSON value nested at most <see cref="MaxDepth"/> levels. A CONNECT's is absent
/// or an object; a DISCONNECT has none; an EVENT's or BINARY_EVENT's is a non-empty array whose first
/// element is a string, the event name; an ACK's or BINARY_ACK's is an array; a CONNECT_ERROR's is an
/// object. In a binary packet every placeholder must be exactly <c>{"_placeholder":true,"num":N}</c>, and
/// each <c>N</c> from 0 to the attachment count less one used exactly once.
/// </remarks>
public static class SocketIoPayloadCheck
{
    /// <summary>The deepest a payload may nest.</summary>
    public const int MaxDepth = 64;

    /// <summary>Judges a payload.</summary>
    /// <param name="type">The packet type.</param>
    /// <param name="payload">The payload, empty when the packet has none.</param>
    /// <param name="attachmentCount">The packet's attachment count, 0 when it is not a binary packet.</param>
    /// <returns><see langword="true"/> when the payload is well formed for the type.</returns>
    public static bool IsValid(SocketIoPacketType type, ReadOnlySpan<byte> payload, int attachmentCount)
    {
        if (payload.IsEmpty)
        {
            return type is SocketIoPacketType.Connect or SocketIoPacketType.Disconnect;
        }

        return type != SocketIoPacketType.Disconnect
            && TryReadShape(payload, attachmentCount, out var first, out var second)
            && HasShapeOf(type, first, second);
    }

    private static bool HasShapeOf(SocketIoPacketType type, JsonTokenType first, JsonTokenType second) => type switch
    {
        SocketIoPacketType.Event or SocketIoPacketType.BinaryEvent =>
            first == JsonTokenType.StartArray && second == JsonTokenType.String,
        SocketIoPacketType.Ack or SocketIoPacketType.BinaryAck => first == JsonTokenType.StartArray,
        _ => first == JsonTokenType.StartObject,
    };

    private static bool TryReadShape(
        ReadOnlySpan<byte> payload,
        int attachmentCount,
        out JsonTokenType first,
        out JsonTokenType second)
    {
        first = JsonTokenType.None;
        second = JsonTokenType.None;
        var placeholders = new SocketIoPlaceholderCheck(attachmentCount);
        var reader = new Utf8JsonReader(payload, new JsonReaderOptions { MaxDepth = MaxDepth });
        var tokens = 0;
        try
        {
            while (reader.Read())
            {
                if (tokens == 0)
                {
                    first = reader.TokenType;
                }
                else if (tokens == 1)
                {
                    second = reader.TokenType;
                }

                tokens++;
                placeholders.Take(ref reader);
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return placeholders.EachUsedOnce();
    }
}
