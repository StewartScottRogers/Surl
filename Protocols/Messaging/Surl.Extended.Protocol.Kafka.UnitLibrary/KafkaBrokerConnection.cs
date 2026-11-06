using System.Collections.Frozen;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers one connection to the <see cref="KafkaBroker"/>, request after request, until it
/// ends. <see cref="KafkaBroker"/> documents the answers.
/// </summary>
/// <param name="connection">The accepted connection.</param>
/// <param name="context">The exchange's context: its limits, log, clock and cancellation token.</param>
/// <param name="authenticationPolicy">Asked once whether an anonymous caller is let in.</param>
/// <param name="answerers">The answerer of each served API that has one.</param>
internal sealed class KafkaBrokerConnection(
    IConnection connection,
    ExchangeContext context,
    IAuthenticationPolicy authenticationPolicy,
    FrozenDictionary<KafkaApiKey, IKafkaApiAnswerer> answerers)
{
    private readonly KafkaRequestFrameReader reader = new(connection, context.Limits.MaxMessageBytes);
    private readonly KafkaConnectionState state = new(connection.TlsSession);
    private bool isAnonymousAllowed;
    private bool isAnyRequestRead;

    /// <summary>Answers the connection until it ends.</summary>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync()
    {
        isAnonymousAllowed = await authenticationPolicy.CheckPasswordLoginAsync(
            new PasswordLogin(context.Scheme, null, null, connection.TlsSession), context.CancellationToken) == PasswordLoginVerdict.AcceptedUnchecked;
        using var headTimeout = context.Limits.HeadTimeout == Timeout.InfiniteTimeSpan
            ? null
            : new CancellationTokenSource(context.Limits.HeadTimeout, context.TimeProvider);

        var frame = await ReadRequestAsync(headTimeout);
        while (frame is not null && Received(frame) is { } request)
        {
            var reply = state.PendingLogin is { CarriesRawTokens: true } login
                ? await AnswerRawTokenAsync(login, request)
                : await AnswerAsync(request);
            if (!await WriteReplyAsync(reply))
            {
                return;
            }

            frame = await ReadRequestAsync(headTimeout);
        }
    }

    // Writes the reply's frame, if any; false when the connection closes after it.
    private async ValueTask<bool> WriteReplyAsync(Reply reply)
    {
        if (reply.Frame is { } bytes)
        {
            await connection.WriteAsync(bytes, context.CancellationToken);
        }

        if (reply.CloseNote is not { } note)
        {
            return true;
        }

        var how = reply.Frame is null ? "closed with no reply" : "closed after the reply";
        context.Log.Note($"{note}; {how}.");
        return false;
    }

    // The head timeout runs from accept until the first request is complete and, when a login
    // is required, until it is accepted (ADR-0105 decisions 5 and 7).
    private async ValueTask<KafkaRequestFrameReadResult?> ReadRequestAsync(CancellationTokenSource? headTimeout)
    {
        var isHeadOver = isAnyRequestRead && (isAnonymousAllowed || state.IsAuthenticated);
        if (headTimeout is null || isHeadOver)
        {
            return await reader.ReadRequestAsync(context.CancellationToken);
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headTimeout.Token);
        try
        {
            var frame = await reader.ReadRequestAsync(cancellation.Token);
            isAnyRequestRead = true;
            return frame;
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note(isAnyRequestRead
                ? "The login was not finished within the head timeout; closed with no reply."
                : "The first request was not complete within the head timeout; closed with no reply.");
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

    // After a v0 SaslHandshake the login travels in raw tokens, each framed like a request, and
    // the server's tokens are framed the same way; a refusal closes with no bytes (ADR-0105
    // decision 5).
    private async ValueTask<Reply> AnswerRawTokenAsync(KafkaSaslLogin login, ReadOnlyMemory<byte> token)
    {
        var step = await login.StepAsync(token, state, context);
        if (step.Outcome == KafkaSaslStepOutcome.Refused)
        {
            return Closed("The SASL login was refused");
        }

        var frame = new KafkaPrimitiveWriter();
        frame.WriteBytes(step.ServerMessage.Span);
        return new(frame.ToArray(), null);
    }

    private async ValueTask<Reply> AnswerAsync(ReadOnlyMemory<byte> request)
    {
        try
        {
            return await AnswerReadableAsync(request);
        }
        catch (KafkaMalformedRequestException exception)
        {
            return Closed($"A request could not be read ({exception.Message})");
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
            return Closed($"API key {fixedFields.ApiKey} is not served");
        }

        if (range.ApiKey == KafkaApiKey.ApiVersions && fixedFields.ApiVersion > range.MaxVersion)
        {
            return Reply.Of(KafkaAnswer.Respond(KafkaApiVersionsFallback.EncodeBody()), fixedFields.CorrelationId, 0);
        }

        if (!range.Serves(fixedFields.ApiVersion))
        {
            return Closed($"{range.ApiKey} version {fixedFields.ApiVersion} is not served ({range.MinVersion} to {range.MaxVersion} are)");
        }

        if (!MayServe(range.ApiKey))
        {
            return Closed($"{range.ApiKey} came before a login");
        }

        if (!answerers.TryGetValue(range.ApiKey, out var answerer))
        {
            return Closed($"{range.ApiKey} is not answered yet");
        }

        var fields = new KafkaPrimitiveReader(request);
        var header = KafkaRequestHeader.Read(fields, range.RequestHeaderVersion(fixedFields.ApiVersion));
        var answer = await answerer.AnswerAsync(new KafkaRequest(header, range, fields, state, connection), context);
        return Reply.Of(answer, header.CorrelationId, range.ResponseHeaderVersion(header.ApiVersion));
    }

    // Secure by default (ADR-0105 decision 5): before a login only ApiVersions and the SASL
    // requests are served, unless the policy lets an anonymous caller in (--allow-anonymous).
    private bool MayServe(KafkaApiKey apiKey) =>
        isAnonymousAllowed
        || state.IsAuthenticated
        || apiKey is KafkaApiKey.ApiVersions or KafkaApiKey.SaslHandshake or KafkaApiKey.SaslAuthenticate;

    private static Reply Closed(string note) => new(null, note);

    // What goes on the wire, if anything, and why the connection closes after it, if it does.
    private readonly record struct Reply(byte[]? Frame, string? CloseNote)
    {
        public static Reply Of(KafkaAnswer answer, int correlationId, short responseHeaderVersion) => new(
            answer.ResponseBody is { } body ? KafkaResponseFrame.Encode(correlationId, responseHeaderVersion, body.Span) : null,
            answer.CloseNote);
    }
}
