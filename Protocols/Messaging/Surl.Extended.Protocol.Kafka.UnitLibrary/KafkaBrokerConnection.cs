using System.Collections.Frozen;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers one connection to the <see cref="KafkaBroker"/>, request after request, until it
/// ends. <see cref="KafkaBroker"/> documents the answers.
/// </summary>
/// <param name="connection">The accepted connection.</param>
/// <param name="context">The exchange's context: its limits, log, clock and cancellation token.</param>
/// <param name="answerers">The answerer of each served API that has one.</param>
internal sealed class KafkaBrokerConnection(
    IConnection connection,
    ExchangeContext context,
    FrozenDictionary<KafkaApiKey, IKafkaApiAnswerer> answerers)
{
    private readonly KafkaRequestFrameReader reader = new(connection, context.Limits.MaxMessageBytes);

    /// <summary>Answers the connection until it ends.</summary>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync()
    {
        var frame = await ReadFirstRequestAsync();
        while (frame is not null && Received(frame) is { } request)
        {
            var reply = await AnswerAsync(request);
            if (reply.Answer.CloseNote is { } note)
            {
                context.Log.Note($"{note}; closed with no reply.");
                return;
            }

            await WriteResponseAsync(reply);
            frame = await reader.ReadRequestAsync(context.CancellationToken);
        }
    }

    // The head timeout runs until the first request is complete (ADR-0105 decision 7).
    private async ValueTask<KafkaRequestFrameReadResult?> ReadFirstRequestAsync()
    {
        if (context.Limits.HeadTimeout == Timeout.InfiniteTimeSpan)
        {
            return await reader.ReadRequestAsync(context.CancellationToken);
        }

        using var headTimeout = new CancellationTokenSource(context.Limits.HeadTimeout, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headTimeout.Token);
        try
        {
            return await reader.ReadRequestAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note("The first request was not complete within the head timeout; closed with no reply.");
            return null;
        }
    }

    // The request's bytes when one was read; otherwise the log notes why there is none.
    private ReadOnlyMemory<byte>? Received(KafkaRequestFrameReadResult frame)
    {
        if (frame.Outcome == KafkaRequestFrameReadOutcome.RequestRead)
        {
            return frame.Request;
        }

        if (frame.Outcome == KafkaRequestFrameReadOutcome.SizeRefused)
        {
            context.Log.Note($"A request's size prefix was {frame.Size}, outside 1 to the request limit {context.Limits.MaxMessageBytes}; closed with no reply.");
        }
        else if (frame.Outcome == KafkaRequestFrameReadOutcome.ConnectionClosedMidRequest)
        {
            context.Log.Note("The client closed the connection part of the way through a request.");
        }

        return null;
    }

    private async ValueTask<Reply> AnswerAsync(ReadOnlyMemory<byte> request)
    {
        try
        {
            return await AnswerReadableAsync(request);
        }
        catch (KafkaMalformedRequestException exception)
        {
            return new(KafkaAnswer.Close($"A request could not be read ({exception.Message})"), 0, 0);
        }
    }

    // The API key, version and correlation id sit at the start of every header version, so
    // they are read before the header version is known.
    private async ValueTask<Reply> AnswerReadableAsync(ReadOnlyMemory<byte> request)
    {
        var fixedFields = KafkaRequestHeader.Read(new KafkaPrimitiveReader(request), 0);
        var range = KafkaServedApis.Find(fixedFields.ApiKey);
        if (range is null)
        {
            return new(KafkaAnswer.Close($"API key {fixedFields.ApiKey} is not served"), 0, 0);
        }

        if (range.ApiKey == KafkaApiKey.ApiVersions && fixedFields.ApiVersion > range.MaxVersion)
        {
            return new(KafkaAnswer.Respond(KafkaApiVersionsFallback.EncodeBody()), fixedFields.CorrelationId, 0);
        }

        if (!range.Serves(fixedFields.ApiVersion))
        {
            return new(KafkaAnswer.Close($"{range.ApiKey} version {fixedFields.ApiVersion} is not served ({range.MinVersion} to {range.MaxVersion} are)"), 0, 0);
        }

        if (!answerers.TryGetValue(range.ApiKey, out var answerer))
        {
            return new(KafkaAnswer.Close($"{range.ApiKey} is not answered yet"), 0, 0);
        }

        var fields = new KafkaPrimitiveReader(request);
        var header = KafkaRequestHeader.Read(fields, range.RequestHeaderVersion(fixedFields.ApiVersion));
        var answer = await answerer.AnswerAsync(new KafkaRequest(header, range, fields), context);
        return new(answer, header.CorrelationId, range.ResponseHeaderVersion(header.ApiVersion));
    }

    private async ValueTask WriteResponseAsync(Reply reply)
    {
        if (reply.Answer.ResponseBody is { } body)
        {
            await connection.WriteAsync(KafkaResponseFrame.Encode(reply.CorrelationId, reply.ResponseHeaderVersion, body.Span), context.CancellationToken);
        }
    }

    // An answer with the correlation id and response header version it is written under.
    private readonly record struct Reply(KafkaAnswer Answer, int CorrelationId, short ResponseHeaderVersion);
}
