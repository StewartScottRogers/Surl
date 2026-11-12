using System.Buffers;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Writes a <see cref="HubMessage"/> as one message of the MessagePack hub protocol: a VarInt
/// length, then the message as an array whose first element is its type (HubProtocol.md "MessagePack
/// (MsgPack) encoding" and "Binary encoding"; ADR-0119, decision 3).
/// </summary>
/// <remarks>
/// Headers are always an empty map. A Completion is <c>[3, {}, id, 1, error]</c>, <c>[3, {}, id, 2]</c>
/// or <c>[3, {}, id, 3, result]</c>; a Close is <c>[7, error, allowReconnect]</c>. Values read from a
/// client are written back as the bytes it sent.
/// </remarks>
public static class HubMessagePackWriter
{
    /// <summary>
    /// Writes <paramref name="message"/> and its length prefix.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The message's bytes, length prefix included.</returns>
    public static byte[] Write(HubMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var body = new MessagePackWriter();
        WriteElements(body, message);

        var framed = new ArrayBufferWriter<byte>();
        WriteLengthPrefix(framed, body.Written.Length);
        framed.Write(body.Written);

        return framed.WrittenSpan.ToArray();
    }

    // Seven bits at a time, least significant first, the high bit set while more follow.
    private static void WriteLengthPrefix(ArrayBufferWriter<byte> framed, int length)
    {
        var rest = (uint)length;
        while (rest >= 0x80)
        {
            framed.Write<byte>([(byte)(rest | 0x80)]);
            rest >>= 7;
        }

        framed.Write<byte>([(byte)rest]);
    }

    private static void WriteElements(MessagePackWriter writer, HubMessage message)
    {
        switch (message)
        {
            case HubInvocation invocation:
                WriteInvocation(writer, invocation);
                break;
            case HubStreamItem streamItem:
                WriteHeader(writer, 4, message.Type, streamItem.InvocationId);
                writer.WriteRaw(streamItem.Item.Utf8Json.Span);
                break;
            case HubCompletion completion:
                WriteCompletion(writer, completion);
                break;
            case HubCancelInvocation cancel:
                WriteHeader(writer, 3, message.Type, cancel.InvocationId);
                break;
            case HubClose close:
                writer.WriteArrayHeader(3);
                writer.WriteUnsigned((int)message.Type);
                writer.WriteStringOrNil(close.Error);
                writer.WriteBool(close.AllowReconnect);
                break;
            case HubAck ack:
                WriteSequenceMessage(writer, message.Type, ack.SequenceId);
                break;
            case HubSequence sequence:
                WriteSequenceMessage(writer, message.Type, sequence.SequenceId);
                break;
            default:
                writer.WriteArrayHeader(1);
                writer.WriteUnsigned((int)message.Type);
                break;
        }
    }

    // [type, {}, invocationId, ...]: arrayLength counts every element.
    private static void WriteHeader(MessagePackWriter writer, int arrayLength, HubMessageType type, string? invocationId)
    {
        writer.WriteArrayHeader(arrayLength);
        writer.WriteUnsigned((int)type);
        writer.WriteEmptyMap();
        writer.WriteStringOrNil(invocationId);
    }

    private static void WriteSequenceMessage(MessagePackWriter writer, HubMessageType type, int sequenceId)
    {
        writer.WriteArrayHeader(2);
        writer.WriteUnsigned((int)type);
        writer.WriteUnsigned(sequenceId);
    }

    private static void WriteInvocation(MessagePackWriter writer, HubInvocation invocation)
    {
        WriteHeader(writer, 6, invocation.Type, invocation.InvocationId);
        writer.WriteStringOrNil(invocation.Target);
        writer.WriteArrayHeader(invocation.Arguments.Count);
        foreach (var argument in invocation.Arguments)
        {
            writer.WriteRaw(argument.Utf8Json.Span);
        }

        writer.WriteArrayHeader(invocation.StreamIds.Count);
        foreach (var streamId in invocation.StreamIds)
        {
            writer.WriteStringOrNil(streamId);
        }
    }

    private static void WriteCompletion(MessagePackWriter writer, HubCompletion completion)
    {
        var hasPayload = completion.Error is not null || completion.Result is not null;
        WriteHeader(writer, hasPayload ? 5 : 4, HubMessageType.Completion, completion.InvocationId);
        if (completion.Error is { } error)
        {
            writer.WriteUnsigned(1);
            writer.WriteStringOrNil(error);
        }
        else if (completion.Result is { } result)
        {
            writer.WriteUnsigned(3);
            writer.WriteRaw(result.Utf8Json.Span);
        }
        else
        {
            writer.WriteUnsigned(2);
        }
    }
}
