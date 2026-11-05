namespace Surl.Http3;

/// <summary>An HTTP/3 frame's type and payload length (RFC 9114 section 7.1), read before its payload.</summary>
/// <param name="Type">The frame type; see <see cref="Http3FrameType"/>.</param>
/// <param name="Length">The payload length in bytes.</param>
internal readonly record struct Http3FrameHeader(ulong Type, ulong Length);
