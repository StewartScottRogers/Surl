using System.Buffers.Binary;

namespace Surl.Protocol.Http;

/// <summary>
/// Turns one frame's header and payload into a typed frame (RFC 9113 section 6), or into the
/// malformation that makes it invalid on its own.
/// </summary>
/// <remarks>
/// It checks what each frame type's own section requires of a single frame - its stream,
/// its length, its padding and its values - and nothing that needs connection or stream state,
/// such as a <c>CONTINUATION</c> that follows no <c>HEADERS</c>, or a <c>HEADERS</c> whose
/// priority names its own stream, which the connection checks once it has decoded the field
/// block, so HPACK state stays whole.
/// </remarks>
internal static class Http2FrameParser
{
    private const uint StreamIdMask = 0x7FFFFFFF;
    private const uint MinMaxFrameSize = 16384;
    private const uint MaxMaxFrameSize = Http2FrameHeader.MaxLength;

    private static readonly Func<Http2FrameHeader, byte[], Http2FrameReadResult>[] ParsersByType =
    [
        ParseData,
        ParseHeaders,
        ParsePriority,
        ParseRstStream,
        ParseSettings,
        ParsePushPromise,
        ParsePing,
        ParseGoAway,
        ParseWindowUpdate,
        ParseContinuation,
    ];

    /// <summary>
    /// Whether <paramref name="type"/> is one of the ten frame types RFC 9113 defines.
    /// </summary>
    /// <param name="type">A frame header's type code.</param>
    /// <returns><see langword="true"/> for a defined type.</returns>
    public static bool IsDefinedType(byte type) => type < ParsersByType.Length;

    /// <summary>
    /// Parses a frame of a defined type.
    /// </summary>
    /// <param name="header">The frame header; its type is defined and its length is the payload's.</param>
    /// <param name="payload">The whole payload.</param>
    /// <returns>The frame, or the malformation.</returns>
    public static Http2FrameReadResult Parse(Http2FrameHeader header, byte[] payload) =>
        ParsersByType[header.Type](header, payload);

    private static Http2FrameReadResult ParseData(Http2FrameHeader header, byte[] payload)
    {
        if (header.StreamId == 0)
        {
            return Http2FrameReadResult.Malformed(header, Http2FrameMalformation.DataOnStreamZero);
        }

        var unpadded = Unpad(header, payload, 0);
        return unpadded.Malformation is { } malformation
            ? Http2FrameReadResult.Malformed(header, malformation)
            : Http2FrameReadResult.Read(new Http2DataFrame(
                header.StreamId,
                payload[unpadded.Start..(unpadded.Start + unpadded.Length)],
                Http2FrameFlags.Has(header.Flags, Http2FrameFlags.EndStream),
                unpadded.PaddingLength));
    }

    private static Http2FrameReadResult ParseHeaders(Http2FrameHeader header, byte[] payload)
    {
        if (header.StreamId == 0)
        {
            return Http2FrameReadResult.Malformed(header, Http2FrameMalformation.HeadersOnStreamZero);
        }

        var hasPriority = Http2FrameFlags.Has(header.Flags, Http2FrameFlags.Priority);
        var priorityLength = hasPriority ? Http2Priority.EncodedLength : 0;
        var unpadded = Unpad(header, payload, priorityLength);
        if (unpadded.Malformation is { } malformation)
        {
            return Http2FrameReadResult.Malformed(header, malformation);
        }

        return Http2FrameReadResult.Read(new Http2HeadersFrame(
            header.StreamId,
            payload[(unpadded.Start + priorityLength)..(unpadded.Start + unpadded.Length)],
            Http2FrameFlags.Has(header.Flags, Http2FrameFlags.EndStream),
            Http2FrameFlags.Has(header.Flags, Http2FrameFlags.EndHeaders),
            hasPriority ? Http2Priority.Read(payload.AsSpan(unpadded.Start)) : null,
            unpadded.PaddingLength));
    }

    private static Http2FrameReadResult ParsePriority(Http2FrameHeader header, byte[] payload)
    {
        if (header.StreamId == 0)
        {
            return Http2FrameReadResult.Malformed(header, Http2FrameMalformation.PriorityOnStreamZero);
        }

        if (payload.Length != Http2Priority.EncodedLength)
        {
            return Http2FrameReadResult.Malformed(header, Http2FrameMalformation.PriorityLengthNotFive);
        }

        var priority = Http2Priority.Read(payload);
        return priority.StreamDependency == header.StreamId
            ? Http2FrameReadResult.Malformed(header, Http2FrameMalformation.PriorityDependsOnItself)
            : Http2FrameReadResult.Read(new Http2PriorityFrame(header.StreamId, priority));
    }

    private static Http2FrameReadResult ParseRstStream(Http2FrameHeader header, byte[] payload)
    {
        if (header.StreamId == 0)
        {
            return Http2FrameReadResult.Malformed(header, Http2FrameMalformation.RstStreamOnStreamZero);
        }

        return payload.Length != Http2RstStreamFrame.EncodedPayloadLength
            ? Http2FrameReadResult.Malformed(header, Http2FrameMalformation.RstStreamLengthNotFour)
            : Http2FrameReadResult.Read(new Http2RstStreamFrame(
                header.StreamId, (Http2ErrorCode)BinaryPrimitives.ReadUInt32BigEndian(payload)));
    }

    private static Http2FrameReadResult ParseSettings(Http2FrameHeader header, byte[] payload)
    {
        if (header.StreamId != 0)
        {
            return Http2FrameReadResult.Malformed(header, Http2FrameMalformation.SettingsOnStream);
        }

        if (Http2FrameFlags.Has(header.Flags, Http2FrameFlags.Ack))
        {
            return payload.Length == 0
                ? Http2FrameReadResult.Read(Http2SettingsFrame.Acknowledgement)
                : Http2FrameReadResult.Malformed(header, Http2FrameMalformation.SettingsAckWithPayload);
        }

        return payload.Length % Http2Setting.EncodedLength != 0
            ? Http2FrameReadResult.Malformed(header, Http2FrameMalformation.SettingsLengthNotMultipleOfSix)
            : ParseSettingEntries(header, payload);
    }

    private static Http2FrameReadResult ParseSettingEntries(Http2FrameHeader header, byte[] payload)
    {
        var settings = new Http2Setting[payload.Length / Http2Setting.EncodedLength];
        for (var index = 0; index < settings.Length; index++)
        {
            var entry = payload.AsSpan(index * Http2Setting.EncodedLength);
            settings[index] = new Http2Setting(
                (Http2SettingIdentifier)BinaryPrimitives.ReadUInt16BigEndian(entry),
                BinaryPrimitives.ReadUInt32BigEndian(entry[2..]));
            if (CheckSetting(settings[index]) is { } malformation)
            {
                return Http2FrameReadResult.Malformed(header, malformation);
            }
        }

        return Http2FrameReadResult.Read(new Http2SettingsFrame(false, settings));
    }

    private static Http2FrameMalformation? CheckSetting(Http2Setting setting) => setting.Identifier switch
    {
        Http2SettingIdentifier.EnablePush => WhenAbove(setting.Value, 1, Http2FrameMalformation.SettingsEnablePushNotZeroOrOne),
        Http2SettingIdentifier.InitialWindowSize => WhenAbove(setting.Value, int.MaxValue, Http2FrameMalformation.SettingsInitialWindowSizeTooLarge),
        Http2SettingIdentifier.MaxFrameSize => CheckMaxFrameSize(setting.Value),
        _ => null,
    };

    private static Http2FrameMalformation? WhenAbove(uint value, uint limit, Http2FrameMalformation malformation) =>
        value > limit ? malformation : null;

    private static Http2FrameMalformation? CheckMaxFrameSize(uint value) =>
        value is >= MinMaxFrameSize and <= MaxMaxFrameSize ? null : Http2FrameMalformation.SettingsMaxFrameSizeOutOfRange;

    private static Http2FrameReadResult ParsePushPromise(Http2FrameHeader header, byte[] payload)
    {
        if (header.StreamId == 0)
        {
            return Http2FrameReadResult.Malformed(header, Http2FrameMalformation.PushPromiseOnStreamZero);
        }

        var unpadded = Unpad(header, payload, Http2PushPromiseFrame.PromisedStreamIdLength);
        if (unpadded.Malformation is { } malformation)
        {
            return Http2FrameReadResult.Malformed(header, malformation);
        }

        return Http2FrameReadResult.Read(new Http2PushPromiseFrame(
            header.StreamId,
            (int)(BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(unpadded.Start)) & StreamIdMask),
            payload[(unpadded.Start + Http2PushPromiseFrame.PromisedStreamIdLength)..(unpadded.Start + unpadded.Length)],
            Http2FrameFlags.Has(header.Flags, Http2FrameFlags.EndHeaders),
            unpadded.PaddingLength));
    }

    private static Http2FrameReadResult ParsePing(Http2FrameHeader header, byte[] payload)
    {
        if (header.StreamId != 0)
        {
            return Http2FrameReadResult.Malformed(header, Http2FrameMalformation.PingOnStream);
        }

        return payload.Length != Http2PingFrame.EncodedPayloadLength
            ? Http2FrameReadResult.Malformed(header, Http2FrameMalformation.PingLengthNotEight)
            : Http2FrameReadResult.Read(new Http2PingFrame(
                Http2FrameFlags.Has(header.Flags, Http2FrameFlags.Ack), BinaryPrimitives.ReadUInt64BigEndian(payload)));
    }

    private static Http2FrameReadResult ParseGoAway(Http2FrameHeader header, byte[] payload)
    {
        if (header.StreamId != 0)
        {
            return Http2FrameReadResult.Malformed(header, Http2FrameMalformation.GoAwayOnStream);
        }

        return payload.Length < Http2GoAwayFrame.FixedPayloadLength
            ? Http2FrameReadResult.Malformed(header, Http2FrameMalformation.GoAwayTooShort)
            : Http2FrameReadResult.Read(new Http2GoAwayFrame(
                (int)(BinaryPrimitives.ReadUInt32BigEndian(payload) & StreamIdMask),
                (Http2ErrorCode)BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(4)),
                payload[Http2GoAwayFrame.FixedPayloadLength..]));
    }

    private static Http2FrameReadResult ParseWindowUpdate(Http2FrameHeader header, byte[] payload)
    {
        if (payload.Length != Http2WindowUpdateFrame.EncodedPayloadLength)
        {
            return Http2FrameReadResult.Malformed(header, Http2FrameMalformation.WindowUpdateLengthNotFour);
        }

        var increment = (int)(BinaryPrimitives.ReadUInt32BigEndian(payload) & StreamIdMask);
        if (increment != 0)
        {
            return Http2FrameReadResult.Read(new Http2WindowUpdateFrame(header.StreamId, increment));
        }

        return Http2FrameReadResult.Malformed(
            header,
            header.StreamId == 0
                ? Http2FrameMalformation.WindowUpdateIncrementZeroOnConnection
                : Http2FrameMalformation.WindowUpdateIncrementZeroOnStream);
    }

    private static Http2FrameReadResult ParseContinuation(Http2FrameHeader header, byte[] payload) =>
        header.StreamId == 0
            ? Http2FrameReadResult.Malformed(header, Http2FrameMalformation.ContinuationOnStreamZero)
            : Http2FrameReadResult.Read(new Http2ContinuationFrame(
                header.StreamId, payload, Http2FrameFlags.Has(header.Flags, Http2FrameFlags.EndHeaders)));

    /// <summary>
    /// Finds the fields between a padded frame's <c>Pad Length</c> byte and its padding.
    /// </summary>
    /// <returns>
    /// Where those fields start and how long they are (fixed fields included), and the padding
    /// length; or the malformation when the payload cannot hold the <c>Pad Length</c> byte and the
    /// fixed fields, or the padding leaves no room for the fixed fields.
    /// </returns>
    private static (Http2FrameMalformation? Malformation, int Start, int Length, int? PaddingLength) Unpad(
        Http2FrameHeader header, byte[] payload, int fixedFieldsLength)
    {
        var padded = Http2FrameFlags.Has(header.Flags, Http2FrameFlags.Padded);
        var start = padded ? 1 : 0;
        if (payload.Length < start + fixedFieldsLength)
        {
            return (Http2FrameMalformation.PayloadTooShortForFields, 0, 0, null);
        }

        int? paddingLength = padded ? payload[0] : null;
        var length = payload.Length - start - (paddingLength ?? 0);
        return length < fixedFieldsLength
            ? (Http2FrameMalformation.PaddingTooLong, 0, 0, null)
            : (null, start, length, paddingLength);
    }
}
