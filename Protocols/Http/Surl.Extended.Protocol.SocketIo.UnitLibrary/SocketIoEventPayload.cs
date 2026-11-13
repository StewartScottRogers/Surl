using System.Text.Json;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// An EVENT's payload, read as a reply needs it (SIO "Sending and receiving data",
/// "Acknowledgement"; ADR-0120, decision 5): whether its name is <c>broadcast</c>, and its
/// arguments - the array without its first element - byte for byte as received.
/// </summary>
internal static class SocketIoEventPayload
{
    /// <summary>
    /// The event name that is sent to every session on the namespace instead of echoed.
    /// </summary>
    public const string BroadcastEventName = "broadcast";

    /// <summary>
    /// Whether the event's name is <see cref="BroadcastEventName"/>.
    /// </summary>
    /// <param name="payload">A non-empty JSON array with a string first, as the reader checked.</param>
    public static bool IsBroadcast(ReadOnlyMemory<byte> payload)
    {
        var reader = ReadName(payload);

        return reader.ValueTextEquals(BroadcastEventName);
    }

    /// <summary>
    /// The event's name, the array's first element (BL-1064).
    /// </summary>
    /// <param name="payload">A non-empty JSON array with a string first, as the reader checked.</param>
    public static string Name(ReadOnlyMemory<byte> payload) => ReadName(payload).GetString()!;

    /// <summary>
    /// The arguments: <c>["hello",1]</c> gives <c>[1]</c>, <c>["hello", 1, 2]</c> gives
    /// <c>[ 1, 2]</c>, and <c>["hello"]</c> gives <c>[]</c>.
    /// </summary>
    /// <param name="payload">A non-empty JSON array with a string first, as the reader checked.</param>
    public static byte[] Arguments(ReadOnlyMemory<byte> payload)
    {
        var reader = ReadName(payload);
        var rest = payload.Span[(int)reader.BytesConsumed..].TrimStart(" \t\r\n"u8);
        if (rest[0] == (byte)',')
        {
            rest = rest[1..];
        }

        return [(byte)'[', .. rest];
    }

    private static Utf8JsonReader ReadName(ReadOnlyMemory<byte> payload)
    {
        var reader = new Utf8JsonReader(payload.Span, new JsonReaderOptions { MaxDepth = SocketIoPayloadCheck.MaxDepth });
        reader.Read();
        reader.Read();

        return reader;
    }
}
