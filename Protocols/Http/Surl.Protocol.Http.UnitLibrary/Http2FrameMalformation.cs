namespace Surl.Protocol.Http;

/// <summary>
/// Each way a frame can break RFC 9113 sections 4 and 6 on its own, before any connection or
/// stream state is consulted. <see cref="Http2FrameMalformations"/> names the error each maps to.
/// </summary>
internal enum Http2FrameMalformation
{
    /// <summary>The payload is longer than the advertised <c>SETTINGS_MAX_FRAME_SIZE</c> (section 4.2).</summary>
    FrameTooLarge,

    /// <summary>A padded <c>DATA</c>, <c>HEADERS</c> or <c>PUSH_PROMISE</c> is too short for its fixed fields (section 4.2).</summary>
    PayloadTooShortForFields,

    /// <summary>The padding is as long as the payload, or longer, leaving no room for the fields (sections 6.1, 6.2 and 6.6).</summary>
    PaddingTooLong,

    /// <summary><c>DATA</c> on stream 0 (section 6.1).</summary>
    DataOnStreamZero,

    /// <summary><c>HEADERS</c> on stream 0 (section 6.2).</summary>
    HeadersOnStreamZero,

    /// <summary><c>PRIORITY</c> on stream 0 (section 6.3).</summary>
    PriorityOnStreamZero,

    /// <summary><c>PRIORITY</c> whose length is not 5 (section 6.3).</summary>
    PriorityLengthNotFive,

    /// <summary><c>PRIORITY</c> naming its own stream as its dependency (section 5.3.1).</summary>
    PriorityDependsOnItself,

    /// <summary><c>RST_STREAM</c> on stream 0 (section 6.4).</summary>
    RstStreamOnStreamZero,

    /// <summary><c>RST_STREAM</c> whose length is not 4 (section 6.4).</summary>
    RstStreamLengthNotFour,

    /// <summary><c>SETTINGS</c> on a stream other than 0 (section 6.5).</summary>
    SettingsOnStream,

    /// <summary><c>SETTINGS</c> with <c>ACK</c> and a payload (section 6.5).</summary>
    SettingsAckWithPayload,

    /// <summary><c>SETTINGS</c> whose length is not a multiple of 6 (section 6.5).</summary>
    SettingsLengthNotMultipleOfSix,

    /// <summary><c>SETTINGS_ENABLE_PUSH</c> other than 0 or 1 (section 6.5.2).</summary>
    SettingsEnablePushNotZeroOrOne,

    /// <summary><c>SETTINGS_INITIAL_WINDOW_SIZE</c> above 2^31-1 (section 6.5.2).</summary>
    SettingsInitialWindowSizeTooLarge,

    /// <summary><c>SETTINGS_MAX_FRAME_SIZE</c> outside 16384 to 16777215 (section 6.5.2).</summary>
    SettingsMaxFrameSizeOutOfRange,

    /// <summary><c>PUSH_PROMISE</c> on stream 0 (section 6.6).</summary>
    PushPromiseOnStreamZero,

    /// <summary><c>PING</c> on a stream other than 0 (section 6.7).</summary>
    PingOnStream,

    /// <summary><c>PING</c> whose length is not 8 (section 6.7).</summary>
    PingLengthNotEight,

    /// <summary><c>GOAWAY</c> on a stream other than 0 (section 6.8).</summary>
    GoAwayOnStream,

    /// <summary><c>GOAWAY</c> shorter than its 8 fixed bytes (section 4.2).</summary>
    GoAwayTooShort,

    /// <summary><c>WINDOW_UPDATE</c> whose length is not 4 (section 6.9).</summary>
    WindowUpdateLengthNotFour,

    /// <summary><c>WINDOW_UPDATE</c> with increment 0 on stream 0 (section 6.9).</summary>
    WindowUpdateIncrementZeroOnConnection,

    /// <summary><c>WINDOW_UPDATE</c> with increment 0 on a stream (section 6.9).</summary>
    WindowUpdateIncrementZeroOnStream,

    /// <summary><c>CONTINUATION</c> on stream 0 (section 6.10).</summary>
    ContinuationOnStreamZero,
}
