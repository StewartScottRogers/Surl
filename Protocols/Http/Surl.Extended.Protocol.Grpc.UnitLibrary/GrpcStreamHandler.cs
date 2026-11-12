using Surl.Http2;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// The gRPC server on one HTTP/2 connection (PROTOCOL-HTTP2; ADR-0116 decisions 0 and 2 to 8): judges
/// each request stream's headers, then serves the call with the named <see cref="GrpcMethod" /> within
/// its deadline, and ends it with a <c>grpc-status</c>.
/// </summary>
/// <remarks>
/// <para>
/// A <c>:method</c> other than <c>POST</c> is HTTP <c>405</c> with <c>allow: POST</c>, and a
/// <c>content-type</c> not beginning <c>application/grpc</c> is HTTP <c>415</c>; neither carries a
/// <c>grpc-status</c>. The rest are gRPC calls, judged in this order, the first failure answered as
/// Trailers-Only: the subtype (<c>application/grpc</c> or <c>application/grpc+proto</c>, parameters
/// ignored), <c>te: trailers</c>, authentication through the connection's
/// <see cref="IHttpAuthenticationSession" /> (only Basic and Bearer are shown to it), <c>grpc-timeout</c>,
/// <c>grpc-encoding</c>, then the service and method <c>:path</c> names.
/// </para>
/// <para>
/// The deadline starts once the headers are judged; when it passes the call ends
/// <c>DEADLINE_EXCEEDED</c>. A failure the method throws ends it with
/// <see cref="GrpcStatus.FromException" />'s status, an unexpected one logged. A client that resets
/// the stream or closes the connection first gets nothing more, and the log says <c>CANCELLED</c>.
/// A call still open when the exchange is cancelled - shutdown, <c>--idle-timeout</c> or
/// <c>--max-time</c> - ends <see cref="GrpcStatus.ServerShuttingDown" /> (<c>UNAVAILABLE</c>), or
/// <c>RST_STREAM</c> <c>CANCEL</c> when a reply was cut off mid-write, before the connection's
/// <c>GOAWAY</c> (ADR-0116 decision 4 and amendment 1).
/// </para>
/// </remarks>
public sealed class GrpcStreamHandler : IHttp2StreamHandler
{
    private const string GrpcMediaType = "application/grpc";
    private const string GrpcProtoMediaType = "application/grpc+proto";

    private readonly Dictionary<string, GrpcService> services;
    private readonly IHttpAuthenticationSession authentication;
    private readonly ExchangeContext context;
    private readonly int maximumMessageLength;
    private readonly long maximumCallLength;

    /// <summary>
    /// Creates the gRPC server for one connection.
    /// </summary>
    /// <param name="services">The services served, each name once.</param>
    /// <param name="authentication">The connection's authentication session (ADR-0032 section 6).</param>
    /// <param name="context">The exchange: its clock, log and limits (<c>--max-message</c>, <c>--max-filesize</c>; 0 is no limit).</param>
    public GrpcStreamHandler(IEnumerable<GrpcService> services, IHttpAuthenticationSession authentication, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(context);
        this.services = services.ToDictionary(service => service.Name, StringComparer.Ordinal);
        this.authentication = authentication;
        this.context = context;
        var maxMessage = context.Limits.MaxMessageBytes;
        maximumMessageLength = maxMessage is 0 or > int.MaxValue ? int.MaxValue : (int)maxMessage;
        maximumCallLength = context.Limits.MaxUploadBytes is 0 ? long.MaxValue : context.Limits.MaxUploadBytes;
    }

    /// <summary>
    /// Answers the request on <paramref name="stream" /> as the remarks say.
    /// </summary>
    /// <param name="stream">The request stream.</param>
    /// <param name="cancellationToken">Cancelled when the client resets the stream or the connection ends.</param>
    /// <returns>A task that completes when the stream is answered.</returns>
    public async Task ServeStreamAsync(Http2RequestStream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var head = stream.Head;
        if (head.Method != "POST")
        {
            await stream.WriteHeadersAsync([new(":status", "405"), new("allow", "POST")], endStream: true);
            return;
        }

        var contentType = FirstValue(head, "content-type");
        if (contentType is null || !contentType.StartsWith(GrpcMediaType, StringComparison.OrdinalIgnoreCase))
        {
            await stream.WriteHeadersAsync([new(":status", "415")], endStream: true);
            return;
        }

        await ServeCallAsync(stream, MediaType(contentType), cancellationToken);
    }

    private async Task ServeCallAsync(Http2RequestStream stream, string mediaType, CancellationToken cancellationToken)
    {
        var served = IsServedMediaType(mediaType);
        var writer = new GrpcResponseWriter(stream, served ? mediaType.ToLowerInvariant() : GrpcMediaType);
        context.Log.Note($"gRPC call {stream.Head.Target}, metadata: {string.Join(", ", stream.Head.Fields.Select(field => field.Key))}");
        GrpcCallException end;
        try
        {
            if (!served)
            {
                throw new GrpcCallException(GrpcStatus.ContentTypeNotServed);
            }

            var status = await AnswerAsync(stream, writer, cancellationToken);
            end = new GrpcCallException(status);
        }
        catch (GrpcCallException failure)
        {
            end = failure;
        }
        catch (OperationCanceledException) when (stream.IsExchangeCancelled)
        {
            context.Log.Note($"gRPC call {stream.Head.Target} ended UNAVAILABLE: the exchange was cancelled while it was open.");
            await writer.FinishAtShutdownAsync();
            return;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || stream.HasRequestBodyEndedEarly)
        {
            context.Log.Note($"gRPC call {stream.Head.Target} CANCELLED by the client.");
            return;
        }

        await writer.FinishAsync(end.Status, end.TrailerMetadata);
    }

    private async Task<GrpcStatus> AnswerAsync(Http2RequestStream stream, GrpcResponseWriter writer, CancellationToken cancellationToken)
    {
        var head = stream.Head;
        if (!Values(head, "te").Contains("trailers", StringComparer.OrdinalIgnoreCase))
        {
            throw new GrpcCallException(GrpcStatus.TeTrailersRequired);
        }

        await AuthenticateAsync(head, cancellationToken);
        var timeout = GrpcTimeout.Read(Values(head, "grpc-timeout"));
        var encoding = GrpcMessageDecompression.ReadEncoding(FirstValue(head, "grpc-encoding"));
        var method = FindMethod(head.Target);
        var requests = new GrpcRequestReader(stream, encoding, maximumMessageLength, maximumCallLength);
        return await ServeWithinDeadlineAsync(method, requests, writer, timeout, cancellationToken);
    }

    private async Task AuthenticateAsync(Http2RequestHead head, CancellationToken cancellationToken)
    {
        var credentials = FirstValue(head, "authorization");
        if (credentials is not null && !IsBasicOrBearer(credentials))
        {
            throw new GrpcCallException(GrpcStatus.Unauthenticated);
        }

        var verdict = await authentication.JudgeAsync(new HttpAuthenticationRequest("POST", head.Target, IsWrite: false, head.Fields), cancellationToken);
        switch (verdict.Outcome)
        {
            case HttpAuthenticationOutcome.Proceed:
                return;
            case HttpAuthenticationOutcome.Challenge:
                throw new GrpcCallException(
                    GrpcStatus.Unauthenticated,
                    [.. verdict.WwwAuthenticateValues.Select(value => new KeyValuePair<string, string>("www-authenticate", value))]);
            default:
                throw new GrpcCallException(GrpcStatus.PermissionDenied);
        }
    }

    private GrpcMethod FindMethod(string path)
    {
        // Surl.Http2 has already refused a :path not beginning with "/" (RFC 9113 section 8.3.1).
        var segments = path.Split('/');
        if (segments.Length != 3 || !services.TryGetValue(segments[1], out var service))
        {
            throw new GrpcCallException(GrpcStatus.UnknownService);
        }

        return service.Methods.FirstOrDefault(method => method.Name == segments[2])
            ?? throw new GrpcCallException(GrpcStatus.UnknownMethod);
    }

    private async Task<GrpcStatus> ServeWithinDeadlineAsync(
        GrpcMethod method,
        GrpcRequestReader requests,
        GrpcResponseWriter writer,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        using var deadline = new GrpcDeadline(timeout, context.TimeProvider, cancellationToken);
        try
        {
            deadline.Token.ThrowIfCancellationRequested();
            var readRequestAsync = method.IsClientStreaming
                ? requests.ReadAsync
                : await ReadTheOneRequestAsync(requests, deadline.Token);
            var call = new GrpcCall(readRequestAsync, writer, context.TimeProvider, maximumMessageLength);
            return await method.ServeAsync(call, deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !requests.HasClientLeft)
        {
            return GrpcStatus.DeadlineExceeded;
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            var status = GrpcStatus.FromException(failure);
            if (status == GrpcStatus.ServerError)
            {
                context.Log.Note($"gRPC call ended UNKNOWN: the service threw {failure.GetType().Name}: {failure.Message}");
            }

            return status;
        }
    }

    private static async Task<Func<CancellationToken, Task<ReadOnlyMemory<byte>?>>> ReadTheOneRequestAsync(
        GrpcRequestReader requests,
        CancellationToken cancellationToken)
    {
        var first = await requests.ReadAsync(cancellationToken);
        if (first is null || await requests.ReadAsync(cancellationToken) is not null)
        {
            throw new GrpcCallException(GrpcStatus.RequestCardinalityViolation);
        }

        var pending = new Queue<ReadOnlyMemory<byte>>([first.Value]);
        return _ => Task.FromResult(pending.TryDequeue(out var request) ? request : (ReadOnlyMemory<byte>?)null);
    }

    private static bool IsBasicOrBearer(string credentials) =>
        credentials.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase)
        || credentials.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

    private static string MediaType(string contentType) => contentType.Split(';', 2)[0].Trim();

    private static bool IsServedMediaType(string mediaType) =>
        mediaType.Equals(GrpcMediaType, StringComparison.OrdinalIgnoreCase)
        || mediaType.Equals(GrpcProtoMediaType, StringComparison.OrdinalIgnoreCase);

    private static string? FirstValue(Http2RequestHead head, string name) => Values(head, name).FirstOrDefault();

    private static List<string> Values(Http2RequestHead head, string name) =>
        [.. head.Fields.Where(field => field.Key == name).Select(field => field.Value)];
}
