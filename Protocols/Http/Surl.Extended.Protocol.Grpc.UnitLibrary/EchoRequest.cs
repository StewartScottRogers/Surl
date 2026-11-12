namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// <c>surl.test.EchoRequest</c> (ADR-0116 decision 1), read from its protobuf bytes and judged against
/// the service's ranges.
/// </summary>
/// <param name="Message">Field 1, <c>message</c>.</param>
/// <param name="Count">Field 2, <c>count</c>: how many replies <c>Expand</c> sends, 0 to 1000.</param>
/// <param name="StatusCode">Field 3, <c>status_code</c>: 0 for OK, or 1 to 16 to end the call with that status.</param>
/// <param name="StatusMessage">Field 4, <c>status_message</c>: the <c>grpc-message</c> sent with <see cref="StatusCode" />.</param>
/// <param name="DelayMilliseconds">Field 5, <c>delay_milliseconds</c>: the wait before answering, 0 to 60000.</param>
internal sealed record EchoRequest(string Message, uint Count, int StatusCode, string StatusMessage, uint DelayMilliseconds)
{
    /// <summary>The most replies <c>Expand</c> sends.</summary>
    public const uint MaximumCount = 1000;

    /// <summary>The longest delay, in milliseconds.</summary>
    public const uint MaximumDelayMilliseconds = 60000;

    /// <summary>
    /// The status the request asks the call to end with, or <see langword="null" /> for none.
    /// </summary>
    public GrpcStatus? RequestedStatus => StatusCode == 0 ? null : new GrpcStatus((GrpcStatusCode)StatusCode, StatusMessage);

    /// <summary>Reads a request, skipping unknown fields as the encoding guide says.</summary>
    /// <param name="payload">The protobuf bytes.</param>
    /// <returns>The request.</returns>
    /// <exception cref="ProtobufFormatException">The bytes are not protobuf, or a known field has the wrong wire type or is not UTF-8.</exception>
    /// <exception cref="GrpcCallException"><c>INVALID_ARGUMENT</c>: a field is out of range.</exception>
    public static EchoRequest Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new ProtobufReader(payload);
        var request = new EchoRequest(string.Empty, 0, 0, string.Empty, 0);
        while (reader.TryReadField(out var field))
        {
            request = request.With(field);
        }

        return request.IsOutOfRange
            ? throw new GrpcCallException(GrpcStatus.RequestFieldOutOfRange)
            : request;
    }

    private bool IsOutOfRange => Count > MaximumCount || StatusCode is < 0 or > 16 || DelayMilliseconds > MaximumDelayMilliseconds;

    /// <summary>Waits <see cref="DelayMilliseconds" /> on <paramref name="timeProvider" />.</summary>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="cancellationToken">Ends the wait early: the deadline, or the client leaving.</param>
    /// <returns>A task that completes when the delay has passed.</returns>
    public Task DelayAsync(TimeProvider timeProvider, CancellationToken cancellationToken) =>
        DelayMilliseconds == 0
            ? Task.CompletedTask
            : Task.Delay(TimeSpan.FromMilliseconds(DelayMilliseconds), timeProvider, cancellationToken);

    private EchoRequest With(ProtobufField field) => field.FieldNumber switch
    {
        1 => this with { Message = field.ReadString() },
        2 => this with { Count = field.ReadUInt32() },
        3 => this with { StatusCode = field.ReadInt32() },
        4 => this with { StatusMessage = field.ReadString() },
        5 => this with { DelayMilliseconds = field.ReadUInt32() },
        _ => this,
    };
}
