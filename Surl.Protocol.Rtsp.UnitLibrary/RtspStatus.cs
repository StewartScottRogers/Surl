using Surl.HttpMessage;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// The statuses the RTSP server answers with, each with the reason phrase ADR-0074 decision 2
/// and 5 name: RFC 2326 section 7.1.1's where RTSP has its own (<c>408 Request Time-out</c>,
/// <c>413 Request Entity Too Large</c>, the <c>45x</c> session statuses, <c>461 Unsupported
/// Transport</c>, <c>505 RTSP Version Not Supported</c>), HTTP's otherwise.
/// </summary>
internal static class RtspStatus
{
    public static HttpStatus Ok { get; } = HttpStatus.Ok;

    public static HttpStatus BadRequest { get; } = HttpStatus.BadRequest;

    public static HttpStatus Unauthorized { get; } = HttpStatus.Unauthorized;

    public static HttpStatus Forbidden { get; } = HttpStatus.Forbidden;

    public static HttpStatus NotFound { get; } = HttpStatus.NotFound;

    public static HttpStatus RequestTimeOut { get; } = new(408, "Request Time-out");

    public static HttpStatus RequestEntityTooLarge { get; } = new(413, "Request Entity Too Large");

    public static HttpStatus RequestHeaderFieldsTooLarge { get; } = HttpStatus.RequestHeaderFieldsTooLarge;

    public static HttpStatus ParameterNotUnderstood { get; } = new(451, "Parameter Not Understood");

    public static HttpStatus SessionNotFound { get; } = new(454, "Session Not Found");

    public static HttpStatus MethodNotValidInThisState { get; } = new(455, "Method Not Valid in This State");

    public static HttpStatus InvalidRange { get; } = new(457, "Invalid Range");

    public static HttpStatus UnsupportedTransport { get; } = new(461, "Unsupported Transport");

    public static HttpStatus NotImplemented { get; } = HttpStatus.NotImplemented;

    public static HttpStatus ServiceUnavailable { get; } = HttpStatus.ServiceUnavailable;

    public static HttpStatus RtspVersionNotSupported { get; } = new(505, "RTSP Version Not Supported");
}
