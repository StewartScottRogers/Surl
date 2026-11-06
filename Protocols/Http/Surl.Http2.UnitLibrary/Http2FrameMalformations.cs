namespace Surl.Http2;

/// <summary>
/// The RFC 9113 error each <see cref="Http2FrameMalformation"/> maps to: its error code, and
/// whether it is a connection error (<c>GOAWAY</c>, section 5.4.1) or a stream error
/// (<c>RST_STREAM</c>, section 5.4.2).
/// </summary>
/// <remarks>
/// A frame size error is a connection error throughout, as ADR-0081 decision 5 answers an
/// oversized frame, and section 5.4 lets any stream error be one. Only the three malformations
/// whose whole payload has been read and which touch one stream alone are stream errors, so the
/// connection can go on reading after them.
/// </remarks>
internal static class Http2FrameMalformations
{
    private static readonly (Http2ErrorCode ErrorCode, bool IsConnectionError)[] Errors =
    [
        (Http2ErrorCode.FrameSizeError, true),     // FrameTooLarge
        (Http2ErrorCode.FrameSizeError, true),     // PayloadTooShortForFields
        (Http2ErrorCode.ProtocolError, true),      // PaddingTooLong
        (Http2ErrorCode.ProtocolError, true),      // DataOnStreamZero
        (Http2ErrorCode.ProtocolError, true),      // HeadersOnStreamZero
        (Http2ErrorCode.ProtocolError, true),      // PriorityOnStreamZero
        (Http2ErrorCode.FrameSizeError, false),    // PriorityLengthNotFive
        (Http2ErrorCode.ProtocolError, false),     // PriorityDependsOnItself
        (Http2ErrorCode.ProtocolError, true),      // RstStreamOnStreamZero
        (Http2ErrorCode.FrameSizeError, true),     // RstStreamLengthNotFour
        (Http2ErrorCode.ProtocolError, true),      // SettingsOnStream
        (Http2ErrorCode.FrameSizeError, true),     // SettingsAckWithPayload
        (Http2ErrorCode.FrameSizeError, true),     // SettingsLengthNotMultipleOfSix
        (Http2ErrorCode.ProtocolError, true),      // SettingsEnablePushNotZeroOrOne
        (Http2ErrorCode.FlowControlError, true),   // SettingsInitialWindowSizeTooLarge
        (Http2ErrorCode.ProtocolError, true),      // SettingsMaxFrameSizeOutOfRange
        (Http2ErrorCode.ProtocolError, true),      // PushPromiseOnStreamZero
        (Http2ErrorCode.ProtocolError, true),      // PingOnStream
        (Http2ErrorCode.FrameSizeError, true),     // PingLengthNotEight
        (Http2ErrorCode.ProtocolError, true),      // GoAwayOnStream
        (Http2ErrorCode.FrameSizeError, true),     // GoAwayTooShort
        (Http2ErrorCode.FrameSizeError, true),     // WindowUpdateLengthNotFour
        (Http2ErrorCode.ProtocolError, true),      // WindowUpdateIncrementZeroOnConnection
        (Http2ErrorCode.ProtocolError, false),     // WindowUpdateIncrementZeroOnStream
        (Http2ErrorCode.ProtocolError, true),      // ContinuationOnStreamZero
    ];

    /// <summary>
    /// The error code <paramref name="malformation"/> is answered with.
    /// </summary>
    /// <param name="malformation">The malformation.</param>
    /// <returns>Its RFC 9113 error code.</returns>
    public static Http2ErrorCode ErrorCodeOf(Http2FrameMalformation malformation) => Errors[(int)malformation].ErrorCode;

    /// <summary>
    /// Whether <paramref name="malformation"/> ends the connection (<c>GOAWAY</c>) rather than
    /// one stream (<c>RST_STREAM</c>).
    /// </summary>
    /// <param name="malformation">The malformation.</param>
    /// <returns><see langword="true"/> for a connection error.</returns>
    public static bool IsConnectionError(Http2FrameMalformation malformation) => Errors[(int)malformation].IsConnectionError;
}
