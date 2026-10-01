using Surl.HttpMessage;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// The statuses the RTSP server answers with, each with the reason phrase ADR-0074 decision 2
/// names: RFC 2326 section 7.1.1's where RTSP has its own (<c>408 Request Time-out</c>,
/// <c>413 Request Entity Too Large</c>, <c>505 RTSP Version Not Supported</c>), HTTP's otherwise.
/// </summary>
internal static class RtspStatus
{
    public static HttpStatus Ok { get; } = HttpStatus.Ok;

    public static HttpStatus BadRequest { get; } = HttpStatus.BadRequest;

    public static HttpStatus NotFound { get; } = HttpStatus.NotFound;

    public static HttpStatus RequestTimeOut { get; } = new(408, "Request Time-out");

    public static HttpStatus RequestEntityTooLarge { get; } = new(413, "Request Entity Too Large");

    public static HttpStatus RequestHeaderFieldsTooLarge { get; } = HttpStatus.RequestHeaderFieldsTooLarge;

    public static HttpStatus NotImplemented { get; } = HttpStatus.NotImplemented;

    public static HttpStatus ServiceUnavailable { get; } = HttpStatus.ServiceUnavailable;

    public static HttpStatus RtspVersionNotSupported { get; } = new(505, "RTSP Version Not Supported");
}
