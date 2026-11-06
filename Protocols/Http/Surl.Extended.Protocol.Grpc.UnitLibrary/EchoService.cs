using System.Text;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// The built-in test service <c>surl.test.Echo</c> (ADR-0116 decision 1): <c>Echo</c> (unary),
/// <c>Expand</c> (server streaming), <c>Collect</c> (client streaming) and <c>Chat</c> (bidirectional).
/// Each request may ask for a delay before the answer and a status to end the call with.
/// </summary>
public static class EchoService
{
    /// <summary>The service's fully qualified name.</summary>
    public const string Name = "surl.test.Echo";

    /// <summary>The service, ready to hand to <see cref="GrpcStreamHandler" />.</summary>
    public static GrpcService Service { get; } = new(
        Name,
        [
            new("Echo", IsClientStreaming: false, IsServerStreaming: false, EchoAsync),
            new("Expand", IsClientStreaming: false, IsServerStreaming: true, ExpandAsync),
            new("Collect", IsClientStreaming: true, IsServerStreaming: false, CollectAsync),
            new("Chat", IsClientStreaming: true, IsServerStreaming: true, ChatAsync),
        ]);

    /// <summary>
    /// The file <c>surl/test/echo.proto</c> that describes the service, as ADR-0116 decision 1 prints it,
    /// for server reflection to serve.
    /// </summary>
    public static ProtoFileDescriptor File { get; } = new(
        "surl/test/echo.proto",
        "surl.test",
        [
            new(
                "EchoRequest",
                [
                    new("message", 1, ProtoFieldType.String),
                    new("count", 2, ProtoFieldType.UInt32),
                    new("status_code", 3, ProtoFieldType.Int32),
                    new("status_message", 4, ProtoFieldType.String),
                    new("delay_milliseconds", 5, ProtoFieldType.UInt32),
                ]),
            new(
                "EchoReply",
                [
                    new("message", 1, ProtoFieldType.String),
                    new("index", 2, ProtoFieldType.UInt32),
                ]),
        ],
        [
            new(
                "Echo",
                [
                    new("Echo", ".surl.test.EchoRequest", ".surl.test.EchoReply", IsClientStreaming: false, IsServerStreaming: false),
                    new("Expand", ".surl.test.EchoRequest", ".surl.test.EchoReply", IsClientStreaming: false, IsServerStreaming: true),
                    new("Collect", ".surl.test.EchoRequest", ".surl.test.EchoReply", IsClientStreaming: true, IsServerStreaming: false),
                    new("Chat", ".surl.test.EchoRequest", ".surl.test.EchoReply", IsClientStreaming: true, IsServerStreaming: true),
                ]),
        ]);

    /// <summary>
    /// <c>Echo</c>: after the delay, the requested status with no reply, or one reply with
    /// <c>message</c> copied and <c>index</c> 0.
    /// </summary>
    private static async Task<GrpcStatus> EchoAsync(GrpcCall call, CancellationToken cancellationToken)
    {
        var request = EchoRequest.Read((await call.ReadRequestAsync(cancellationToken))!.Value);
        await request.DelayAsync(call.TimeProvider, cancellationToken);
        if (request.RequestedStatus is { } status)
        {
            return status;
        }

        await call.WriteReplyAsync(Reply(request.Message, 0));
        return GrpcStatus.Ok;
    }

    /// <summary>
    /// <c>Expand</c>: <c>count</c> replies, each after the delay, with <c>index</c> 0, 1, 2, …; then the
    /// requested status, or OK.
    /// </summary>
    private static async Task<GrpcStatus> ExpandAsync(GrpcCall call, CancellationToken cancellationToken)
    {
        var request = EchoRequest.Read((await call.ReadRequestAsync(cancellationToken))!.Value);
        for (uint index = 0; index < request.Count; index++)
        {
            await request.DelayAsync(call.TimeProvider, cancellationToken);
            await call.WriteReplyAsync(Reply(request.Message, index));
        }

        return request.RequestedStatus ?? GrpcStatus.Ok;
    }

    /// <summary>
    /// <c>Collect</c>: every request, each after its delay, until the client half-closes; then one
    /// reply of the messages concatenated with <c>index</c> the number of requests. A request with a
    /// status ends the call with it at once.
    /// </summary>
    private static async Task<GrpcStatus> CollectAsync(GrpcCall call, CancellationToken cancellationToken)
    {
        var collected = new StringBuilder();
        var collectedBytes = 0L;
        uint count = 0;
        while (await call.ReadRequestAsync(cancellationToken) is { } payload)
        {
            var request = EchoRequest.Read(payload);
            await request.DelayAsync(call.TimeProvider, cancellationToken);
            if (request.RequestedStatus is { } status)
            {
                return status;
            }

            collectedBytes += Encoding.UTF8.GetByteCount(request.Message);
            if (collectedBytes > call.MaximumMessageLength)
            {
                return GrpcStatus.ReplyLargerThanTheLimit;
            }

            collected.Append(request.Message);
            count++;
        }

        await call.WriteReplyAsync(Reply(collected.ToString(), count));
        return GrpcStatus.Ok;
    }

    /// <summary>
    /// <c>Chat</c>: one reply per request as it arrives, after its delay, with <c>index</c> its position;
    /// a request with a status ends the call with it at once; OK when the client half-closes.
    /// </summary>
    private static async Task<GrpcStatus> ChatAsync(GrpcCall call, CancellationToken cancellationToken)
    {
        uint index = 0;
        while (await call.ReadRequestAsync(cancellationToken) is { } payload)
        {
            var request = EchoRequest.Read(payload);
            await request.DelayAsync(call.TimeProvider, cancellationToken);
            if (request.RequestedStatus is { } status)
            {
                return status;
            }

            await call.WriteReplyAsync(Reply(request.Message, index++));
        }

        return GrpcStatus.Ok;
    }

    /// <summary>
    /// <c>surl.test.EchoReply</c>: <c>message</c> (1) and <c>index</c> (2), each left out at its proto3
    /// default, as the encoding guide says a proto3 writer does.
    /// </summary>
    private static byte[] Reply(string message, uint index)
    {
        var writer = new ProtobufWriter();
        if (message.Length > 0)
        {
            writer.WriteString(1, message);
        }

        if (index > 0)
        {
            writer.WriteUInt32(2, index);
        }

        return writer.ToArray();
    }
}
