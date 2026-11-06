namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// One method of a served service (ADR-0116 decisions 1 and 2): its name, its call shape, and what
/// answers a call to it.
/// </summary>
/// <param name="Name">The method name, the last segment of <c>:path</c>.</param>
/// <param name="IsClientStreaming">
/// Whether the client may send any number of request messages. When it may not - a unary or
/// server-streaming method - <see cref="GrpcStreamHandler" /> reads the whole request first and ends a
/// call with no request message, or more than one, <c>UNIMPLEMENTED</c>
/// <c>request cardinality violation</c> without calling <paramref name="ServeAsync" />.
/// </param>
/// <param name="IsServerStreaming">Whether the method may reply with any number of messages, as its descriptor says.</param>
/// <param name="ServeAsync">
/// Answers one call: reads its requests and writes its replies through the <see cref="GrpcCall" />, and
/// returns the status the call ends with. The token is cancelled when the deadline passes, the client
/// resets the stream or the connection ends.
/// </param>
public sealed record GrpcMethod(
    string Name,
    bool IsClientStreaming,
    bool IsServerStreaming,
    Func<GrpcCall, CancellationToken, Task<GrpcStatus>> ServeAsync);
