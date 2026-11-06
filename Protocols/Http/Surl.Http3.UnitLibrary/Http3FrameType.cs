namespace Surl.Http3;

/// <summary>
/// The HTTP/3 frame types the server reads and writes (RFC 9114 section 7.2), and the HTTP/2 frame
/// types RFC 9114 section 7.2.8 reserves, which are <c>H3_FRAME_UNEXPECTED</c> wherever they arrive.
/// Every other type is unknown and is skipped (section 9).
/// </summary>
internal static class Http3FrameType
{
    /// <summary><c>DATA</c>: a piece of a message's content.</summary>
    public const ulong Data = 0x00;

    /// <summary><c>HEADERS</c>: a QPACK-encoded field section.</summary>
    public const ulong Headers = 0x01;

    /// <summary><c>CANCEL_PUSH</c>: read and ignored; the server never pushes (ADR-0082 decision 9).</summary>
    public const ulong CancelPush = 0x03;

    /// <summary><c>SETTINGS</c>: the first frame of each control stream, and only there.</summary>
    public const ulong Settings = 0x04;

    /// <summary><c>PUSH_PROMISE</c>: sent only by a server, so the client's is unexpected.</summary>
    public const ulong PushPromise = 0x05;

    /// <summary><c>GOAWAY</c>: the server's names the next request stream it would accept.</summary>
    public const ulong GoAway = 0x07;

    /// <summary><c>MAX_PUSH_ID</c>: read and ignored; the server never pushes.</summary>
    public const ulong MaxPushId = 0x0d;

    /// <summary>Whether <paramref name="type"/> is one of HTTP/2's (<c>PRIORITY</c>, <c>PING</c>, <c>WINDOW_UPDATE</c>, <c>CONTINUATION</c>), which HTTP/3 reserves.</summary>
    /// <param name="type">The frame type.</param>
    /// <returns><see langword="true"/> for 0x02, 0x06, 0x08 and 0x09.</returns>
    public static bool IsReservedHttp2Type(ulong type) => type is 0x02 or 0x06 or 0x08 or 0x09;

    /// <summary>
    /// Whether a frame of <paramref name="type"/> on a request stream is <c>H3_FRAME_UNEXPECTED</c>
    /// (RFC 9114 sections 7.2.3 to 7.2.7): the control frames, the client's <c>PUSH_PROMISE</c> and
    /// HTTP/2's types.
    /// </summary>
    /// <param name="type">The frame type.</param>
    /// <returns><see langword="true"/> when the frame may not arrive on a request stream.</returns>
    public static bool IsUnexpectedOnRequestStream(ulong type) =>
        type is CancelPush or Settings or PushPromise or GoAway or MaxPushId || IsReservedHttp2Type(type);

    /// <summary>
    /// Whether a frame of <paramref name="type"/> on the client's control stream is
    /// <c>H3_FRAME_UNEXPECTED</c> (RFC 9114 sections 7.2.1, 7.2.2 and 7.2.5): <c>DATA</c>,
    /// <c>HEADERS</c>, <c>PUSH_PROMISE</c> and HTTP/2's types. A second <c>SETTINGS</c> is the
    /// control stream's own rule.
    /// </summary>
    /// <param name="type">The frame type.</param>
    /// <returns><see langword="true"/> when the frame may not arrive on the control stream.</returns>
    public static bool IsUnexpectedOnControlStream(ulong type) =>
        type is Data or Headers or PushPromise || IsReservedHttp2Type(type);
}
