namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Reads one message of the MessagePack hub protocol - the bytes after its VarInt length - into a
/// <see cref="HubMessage"/>, or finds it a protocol error (HubProtocol.md "MessagePack (MsgPack)
/// encoding" and "Errors"; ADR-0119, decisions 3 and 7).
/// </summary>
/// <remarks>
/// A message is a protocol error when it is not exactly one array; when its first element is not an
/// integer 1 to 9; when an element the type requires is missing or of the wrong kind (headers are a
/// map, whatever it holds); when an <c>invocationId</c> or stream ID passes
/// <see cref="HubMessageJsonReader.MaxIdLength"/> characters; when a StreamInvocation has a nil
/// <c>invocationId</c>; when a Completion's result kind is not 1, 2 or 3; or when an Ack or Sequence
/// has no non-negative integer. Elements past the ones a type has are skipped. Arguments, items and
/// results are kept as the MessagePack bytes the client sent.
/// </remarks>
public static class HubMessagePackReader
{
    private const int ErrorResult = 1;

    private const int VoidResult = 2;

    private const int ValueResult = 3;

    /// <summary>
    /// Reads <paramref name="record"/>, one message without its length prefix.
    /// </summary>
    /// <param name="record">The message's bytes.</param>
    /// <param name="message">The message read, or <see langword="null"/> on a protocol error.</param>
    /// <returns>Whether the record is a message the server reads.</returns>
    public static bool TryRead(ReadOnlyMemory<byte> record, out HubMessage? message)
    {
        var cursor = new MessagePackCursor(record);
        message = null;
        if (cursor.TryReadArrayCount(out var count) && count > 0 && cursor.TryReadInt(out var type))
        {
            var elements = new Elements(cursor, count - 1);
            message = Build(type, elements);
            message = elements.TrySkipRest() ? message : null;
        }

        return message is not null;
    }

    private static HubMessage? Build(int type, Elements elements) => type switch
    {
        (int)HubMessageType.Invocation or (int)HubMessageType.StreamInvocation => BuildInvocation(type == (int)HubMessageType.StreamInvocation, elements),
        (int)HubMessageType.StreamItem => BuildStreamItem(elements),
        (int)HubMessageType.Completion => BuildCompletion(elements),
        (int)HubMessageType.CancelInvocation => BuildCancelInvocation(elements),
        (int)HubMessageType.Ping => new HubPing(),
        (int)HubMessageType.Close => BuildClose(elements),
        (int)HubMessageType.Ack => elements.TryReadSequenceId(out var ack) ? new HubAck(ack) : null,
        (int)HubMessageType.Sequence => elements.TryReadSequenceId(out var next) ? new HubSequence(next) : null,
        _ => null,
    };

    private static HubInvocation? BuildInvocation(bool isStreaming, Elements elements)
    {
        if (!elements.TrySkipHeaders()
            || !elements.TryReadId(allowNil: !isStreaming, out var invocationId)
            || !elements.TryReadString(out var target)
            || !elements.TryReadValues(out var arguments))
        {
            return null;
        }

        // The stream IDs are absent from a message an older client sends.
        var streamIds = new List<string>();
        var isStreamIdsRead = !elements.HasMore || elements.TryReadIds(streamIds);

        return isStreamIdsRead ? new HubInvocation(isStreaming, invocationId, target!, arguments, streamIds) : null;
    }

    private static HubStreamItem? BuildStreamItem(Elements elements) =>
        elements.TrySkipHeaders() && elements.TryReadId(allowNil: false, out var id) && elements.TryReadValue(out var item)
            ? new HubStreamItem(id!, item)
            : null;

    private static HubCancelInvocation? BuildCancelInvocation(Elements elements) =>
        elements.TrySkipHeaders() && elements.TryReadId(allowNil: false, out var id) ? new HubCancelInvocation(id!) : null;

    private static HubCompletion? BuildCompletion(Elements elements)
    {
        if (!elements.TrySkipHeaders() || !elements.TryReadId(allowNil: false, out var id) || !elements.TryReadInt(out var kind))
        {
            return null;
        }

        return kind switch
        {
            ErrorResult => elements.TryReadString(out var error) ? new HubCompletion(id!, null, error) : null,
            VoidResult => new HubCompletion(id!, null, null),
            ValueResult => elements.TryReadValue(out var result) ? new HubCompletion(id!, result, null) : null,
            _ => null,
        };
    }

    // [7, Error, AllowReconnect]; the last is absent from a message an older client sends.
    private static HubClose? BuildClose(Elements elements)
    {
        if (!elements.TryReadNullableString(out var error))
        {
            return null;
        }

        var allowReconnect = false;

        return !elements.HasMore || elements.TryReadBool(out allowReconnect) ? new HubClose(error, allowReconnect) : null;
    }

    // The elements of one message after its type, each read once, in order.
    private sealed class Elements(MessagePackCursor cursor, int remaining)
    {
        public bool HasMore => remaining > 0;

        public bool TrySkipHeaders() => Take() && cursor.IsNext(MessagePackKind.Map) && cursor.TrySkipValue(out _);

        public bool TryReadId(bool allowNil, out string? id) =>
            Take() && cursor.TryReadString(allowNil, out id) && IsIdLong(id)
            || Clear(out id);

        public bool TryReadString(out string? value) => Take() && cursor.TryReadString(allowNil: false, out value) || Clear(out value);

        public bool TryReadNullableString(out string? value) => Take() && cursor.TryReadString(allowNil: true, out value) || Clear(out value);

        public bool TryReadInt(out int number)
        {
            number = 0;

            return Take() && cursor.TryReadInt(out number);
        }

        // Only asked for after HasMore, so the element is there.
        public bool TryReadBool(out bool value)
        {
            remaining--;

            return cursor.TryReadBool(out value);
        }

        public bool TryReadSequenceId(out int sequenceId) => TryReadInt(out sequenceId) && sequenceId >= 0;

        public bool TryReadValue(out HubJsonValue value)
        {
            value = default;
            if (!Take() || !cursor.TrySkipValue(out var bytes))
            {
                return false;
            }

            value = new HubJsonValue(bytes);

            return true;
        }

        public bool TryReadValues(out List<HubJsonValue> values)
        {
            values = [];
            if (!Take() || !cursor.TryReadArrayCount(out var count))
            {
                return false;
            }

            for (var index = 0; index < count; index++)
            {
                if (!cursor.TrySkipValue(out var bytes))
                {
                    return false;
                }

                values.Add(new HubJsonValue(bytes));
            }

            return true;
        }

        // Only asked for after HasMore, so the element is there.
        public bool TryReadIds(List<string> ids)
        {
            remaining--;
            if (!cursor.TryReadArrayCount(out var count))
            {
                return false;
            }

            for (var index = 0; index < count; index++)
            {
                if (!cursor.TryReadString(allowNil: false, out var id) || !IsIdLong(id))
                {
                    return false;
                }

                ids.Add(id!);
            }

            return true;
        }

        // Skips the elements this type does not use, then checks nothing follows the message.
        public bool TrySkipRest()
        {
            while (remaining > 0 && cursor.TrySkipValue(out _))
            {
                remaining--;
            }

            return remaining == 0 && cursor.IsAtEnd;
        }

        private static bool IsIdLong(string? id) => (id?.Length ?? 0) <= HubMessageJsonReader.MaxIdLength;

        private static bool Clear(out string? value)
        {
            value = null;

            return false;
        }

        private bool Take()
        {
            remaining--;

            return remaining >= 0;
        }
    }
}
