using System.Globalization;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// The status a call ends with: its <c>grpc-status</c> code and, unless the code is OK, its
/// <c>grpc-message</c> text. The texts the library itself sends are the fixed ASCII strings
/// ADR-0116 names, held here as members so one text has one home.
/// </summary>
/// <param name="Code">The status code.</param>
/// <param name="Message">The status message, unencoded; empty for OK.</param>
public sealed record GrpcStatus(GrpcStatusCode Code, string Message)
{
    /// <summary>OK, with no message.</summary>
    public static GrpcStatus Ok { get; } = new(GrpcStatusCode.Ok, string.Empty);

    /// <summary><c>INTERNAL</c>: a request message that is not valid protobuf, or a Length-Prefixed-Message that is not valid framing (ADR-0116 decision 2).</summary>
    public static GrpcStatus RequestMessageDidNotParse { get; } = new(GrpcStatusCode.Internal, "request message did not parse");

    /// <summary><c>UNKNOWN</c>: an exception the service did not expect (ADR-0116 decision 2).</summary>
    public static GrpcStatus ServerError { get; } = new(GrpcStatusCode.Unknown, "server error");

    /// <summary><c>RESOURCE_EXHAUSTED</c>: one message, framed or decompressed, is longer than <c>--max-message</c> (ADR-0116 decision 8).</summary>
    public static GrpcStatus MessageLargerThanTheLimit { get; } = new(GrpcStatusCode.ResourceExhausted, "message larger than the limit");

    /// <summary><c>RESOURCE_EXHAUSTED</c>: a call's request messages together are longer than <c>--max-filesize</c> (ADR-0116 decision 8).</summary>
    public static GrpcStatus CallLargerThanTheLimit { get; } = new(GrpcStatusCode.ResourceExhausted, "call larger than the limit");

    /// <summary><c>INTERNAL</c>: a <c>grpc-timeout</c> that is not 1 to 8 digits and one unit, or more than one of them (ADR-0116 decision 5).</summary>
    public static GrpcStatus MalformedGrpcTimeout { get; } = new(GrpcStatusCode.Internal, "malformed grpc-timeout");

    /// <summary><c>UNIMPLEMENTED</c>: a <c>grpc-encoding</c> other than identity, gzip and deflate (ADR-0116 decision 6).</summary>
    public static GrpcStatus GrpcEncodingNotServed { get; } = new(GrpcStatusCode.Unimplemented, "grpc-encoding not served");

    /// <summary><c>INTERNAL</c>: a message with Compressed-Flag 1 when the call has no <c>grpc-encoding</c> or <c>identity</c> (ADR-0116 decision 6).</summary>
    public static GrpcStatus CompressedFlagSetWithoutGrpcEncoding { get; } = new(GrpcStatusCode.Internal, "compressed flag set without grpc-encoding");

    /// <summary><c>INTERNAL</c>: a compressed message that does not decompress under the call's encoding (ADR-0116 decision 6).</summary>
    public static GrpcStatus MessageDidNotDecompress { get; } = new(GrpcStatusCode.Internal, "message did not decompress");

    /// <summary><c>UNIMPLEMENTED</c>: a <c>content-type</c> of <c>application/grpc</c> with a subtype other than <c>proto</c>, a codec the server lacks (ADR-0116 decision 3).</summary>
    public static GrpcStatus ContentTypeNotServed { get; } = new(GrpcStatusCode.Unimplemented, "content-type not served");

    /// <summary><c>INTERNAL</c>: a call without <c>te: trailers</c> (ADR-0116 decision 3).</summary>
    public static GrpcStatus TeTrailersRequired { get; } = new(GrpcStatusCode.Internal, "te: trailers required");

    /// <summary><c>UNAUTHENTICATED</c>: a call the authentication session challenges, or one with credentials neither Basic nor Bearer (ADR-0116 decision 7).</summary>
    public static GrpcStatus Unauthenticated { get; } = new(GrpcStatusCode.Unauthenticated, "unauthenticated");

    /// <summary><c>PERMISSION_DENIED</c>: a call the authentication session forbids (ADR-0116 decision 7).</summary>
    public static GrpcStatus PermissionDenied { get; } = new(GrpcStatusCode.PermissionDenied, "permission denied");

    /// <summary><c>UNIMPLEMENTED</c>: a <c>:path</c> naming no served service (ADR-0116 decision 3).</summary>
    public static GrpcStatus UnknownService { get; } = new(GrpcStatusCode.Unimplemented, "unknown service");

    /// <summary><c>UNIMPLEMENTED</c>: a <c>:path</c> naming a served service but none of its methods (ADR-0116 decision 3).</summary>
    public static GrpcStatus UnknownMethod { get; } = new(GrpcStatusCode.Unimplemented, "unknown method");

    /// <summary><c>UNIMPLEMENTED</c>: a unary or server-streaming call with no request message, or more than one (ADR-0116 decision 2).</summary>
    public static GrpcStatus RequestCardinalityViolation { get; } = new(GrpcStatusCode.Unimplemented, "request cardinality violation");

    /// <summary><c>DEADLINE_EXCEEDED</c>: the call's <c>grpc-timeout</c> passed before it ended (ADR-0116 decision 5).</summary>
    public static GrpcStatus DeadlineExceeded { get; } = new(GrpcStatusCode.DeadlineExceeded, "deadline exceeded");

    /// <summary><c>UNAVAILABLE</c>: a call still open when the exchange was cancelled - shutdown, <c>--idle-timeout</c> or <c>--max-time</c> (ADR-0116 decision 4).</summary>
    public static GrpcStatus ServerShuttingDown { get; } = new(GrpcStatusCode.Unavailable, "server shutting down");

    /// <summary><c>INVALID_ARGUMENT</c>: a <c>surl.test.Echo</c> request whose <c>count</c>, <c>status_code</c> or <c>delay_milliseconds</c> is out of range (ADR-0116 decision 1).</summary>
    public static GrpcStatus RequestFieldOutOfRange { get; } = new(GrpcStatusCode.InvalidArgument, "request field out of range");

    /// <summary><c>RESOURCE_EXHAUSTED</c>: a reply longer than <c>--max-message</c>, <c>Collect</c>'s concatenation (ADR-0116 decision 8).</summary>
    public static GrpcStatus ReplyLargerThanTheLimit { get; } = new(GrpcStatusCode.ResourceExhausted, "reply larger than the limit");

    /// <summary>
    /// The <c>grpc-status</c> value: the code in decimal, <c>0</c> to <c>16</c>.
    /// </summary>
    public string StatusValue => ((int)Code).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The <c>grpc-message</c> value: <see cref="Message" /> percent-encoded by <see cref="GrpcStatusMessage.Encode" />.
    /// </summary>
    public string MessageValue => GrpcStatusMessage.Encode(Message);

    /// <summary>
    /// The status a failure ends its call with (ADR-0116 decision 2): a <see cref="GrpcCallException" />'s own
    /// status, <see cref="RequestMessageDidNotParse" /> for a <see cref="ProtobufFormatException" />, and
    /// <see cref="ServerError" /> for any other exception.
    /// </summary>
    /// <param name="failure">The exception that ended the call.</param>
    /// <returns>The status to send.</returns>
    public static GrpcStatus FromException(Exception failure) => failure switch
    {
        GrpcCallException call => call.Status,
        ProtobufFormatException => RequestMessageDidNotParse,
        _ => ServerError,
    };
}
