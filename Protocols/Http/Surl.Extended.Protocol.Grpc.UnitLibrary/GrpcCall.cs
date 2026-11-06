namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// One call as its <see cref="GrpcMethod" /> sees it (ADR-0116 decision 2): its request messages,
/// decompressed and judged against the limits, read one at a time, and its replies written as
/// uncompressed Length-Prefixed-Messages. Reading and writing are independent, so a streaming method
/// may reply to each request as it arrives.
/// </summary>
public sealed class GrpcCall
{
    private readonly Func<CancellationToken, Task<ReadOnlyMemory<byte>?>> readRequestAsync;
    private readonly GrpcResponseWriter replies;

    /// <summary>
    /// Creates a call that reads its requests with <paramref name="readRequestAsync" /> and writes its
    /// replies with <paramref name="replies" />.
    /// </summary>
    /// <param name="readRequestAsync">Reads the next request message's payload, or <see langword="null" /> once the client half-closed.</param>
    /// <param name="replies">Writes the response on the stream.</param>
    /// <param name="timeProvider">The clock a method waits on.</param>
    /// <param name="maximumMessageLength">The longest message, <c>--max-message</c>.</param>
    internal GrpcCall(
        Func<CancellationToken, Task<ReadOnlyMemory<byte>?>> readRequestAsync,
        GrpcResponseWriter replies,
        TimeProvider timeProvider,
        int maximumMessageLength)
    {
        this.readRequestAsync = readRequestAsync;
        this.replies = replies;
        TimeProvider = timeProvider;
        MaximumMessageLength = maximumMessageLength;
    }

    /// <summary>The clock a method waits on: the exchange's injected <see cref="System.TimeProvider" />.</summary>
    public TimeProvider TimeProvider { get; }

    /// <summary>The longest message, request or reply, in bytes: <c>--max-message</c> (ADR-0116 decision 8).</summary>
    public int MaximumMessageLength { get; }

    /// <summary>
    /// Reads the next request message's payload, decompressed.
    /// </summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>The payload, or <see langword="null" /> once the client has half-closed.</returns>
    /// <exception cref="GrpcCallException">The message breaks the framing, compression or limit rules.</exception>
    public Task<ReadOnlyMemory<byte>?> ReadRequestAsync(CancellationToken cancellationToken) => readRequestAsync(cancellationToken);

    /// <summary>
    /// Writes one reply message: the Response-Headers first if none were written yet, then the
    /// message as an uncompressed Length-Prefixed-Message (ADR-0116 decision 4).
    /// </summary>
    /// <param name="payload">The reply's protobuf bytes.</param>
    /// <returns>A task that completes when the reply is written.</returns>
    public Task WriteReplyAsync(ReadOnlyMemory<byte> payload) => replies.WriteReplyAsync(payload);
}
