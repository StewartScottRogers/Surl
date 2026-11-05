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
