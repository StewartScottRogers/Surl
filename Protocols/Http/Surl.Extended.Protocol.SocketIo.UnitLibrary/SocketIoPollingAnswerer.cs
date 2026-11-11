using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// Answers the HTTP long-polling transport's requests (EIO "HTTP long-polling", "Packet encoding";
/// ADR-0120, decisions 2 and 7, and amendment 2).
/// </summary>
/// <remarks>
/// <para>
/// The handshake <c>GET</c> opens a session and answers <c>200</c> with its open packet, whose
/// <c>upgrades</c> is <c>["websocket"]</c>. A <c>GET</c> with its <c>sid</c> is held until a packet is
/// queued, then answered with every queued packet joined by <c>0x1E</c>, binary ones <c>b</c> plus
/// base64, <c>Content-Type: text/plain; charset=UTF-8</c>. A <c>POST</c> is read within
/// <c>maxPayload</c> (<c>--max-message</c>), split at <c>0x1E</c> (or taken whole as one binary packet
/// when its <c>Content-Type</c> is <c>application/octet-stream</c>), each packet handed to the session,
/// and answered <c>200</c> <c>text/html</c> <c>ok</c>.
/// </para>
/// <para>
/// A second concurrent <c>GET</c> or <c>POST</c> is code 3 and closes the session; so is a body that
/// is not Engine.IO packets or not read whole, and one past <c>maxPayload</c> is <c>413</c> and closes
/// it. A polling session lives on no connection: its heartbeat and connect timeout end a client that
/// stops polling, and shutdown ends it with nothing more sent.
/// </para>
/// </remarks>
/// <param name="listener">What the listener's sessions share.</param>
/// <param name="context">The exchange that carries the request.</param>
/// <param name="tlsSession">The TLS session of the request's connection, for a session it opens.</param>
/// <param name="answers">How answers are written.</param>
internal sealed class SocketIoPollingAnswerer(SocketIoListener listener, ExchangeContext context, TlsSession? tlsSession, SocketIoAnswers answers)
{
    /// <summary>The <c>Content-Type</c> of a polling <c>GET</c>'s answer.</summary>
    public const string PollContentType = "text/plain; charset=UTF-8";

    /// <summary>The <c>Content-Type</c> of a polling <c>POST</c>'s answer.</summary>
    public const string PostContentType = "text/html";

    private static readonly byte[] PostAnswer = "ok"u8.ToArray();

    /// <summary>
    /// Opens a polling session and answers with its open packet, or <c>503</c> when the listener holds
    /// <see cref="SocketIoSessions.MaxSessions"/>.
    /// </summary>
    public HttpAnswer Open(HttpRequest request)
    {
        if (listener.Sessions.TryReserve() is not { } sessionId)
        {
            context.Log.Note($"Handshake refused: the listener holds {SocketIoSessions.MaxSessions} sessions.");

            return answers.Empty(request, HttpStatus.ServiceUnavailable);
        }

        var session = new SocketIoSession(sessionId, listener, context, tlsSession, SocketIoTransportState.Polling);
        listener.Sessions.Attach(sessionId, session);
        session.Start();
        _ = ForgetWhenFinishedAsync(sessionId, session);

        return answers.WithBody(request, HttpStatus.Ok, PollContentType, EngineIoPollingPayload.Join([session.OpenPacket()]));
    }

    /// <summary>
    /// Answers a <c>GET</c> or <c>POST</c> carrying <paramref name="session"/>'s <c>sid</c>; any other
    /// method is code 3.
    /// </summary>
    public Task<HttpAnswer> AnswerAsync(HttpRequest request, SocketIoSession session, CancellationToken cancellationToken) => request.Method switch
    {
        "GET" => PollAsync(request, session, cancellationToken),
        "POST" => PostAsync(request, session, cancellationToken),
        _ => Task.FromResult(answers.Refused(request, EngineIoRefusal.BadRequest)),
    };

    private static bool IsBinaryBody(HttpRequest request) =>
        request.Fields.FirstOrDefault(field => string.Equals(field.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)).Value is { } type
        && type.StartsWith("application/octet-stream", StringComparison.OrdinalIgnoreCase);

    private async Task ForgetWhenFinishedAsync(string sessionId, SocketIoSession session)
    {
        using (context.ShutdownToken.Register(() => session.Decide(SocketIoSessionEnd.Silent("the server is shutting down"))))
        {
            await session.Finished;
        }

        listener.Sessions.Remove(sessionId);
    }

    private async Task<HttpAnswer> PollAsync(HttpRequest request, SocketIoSession session, CancellationToken cancellationToken)
    {
        var packets = await session.PollAsync(cancellationToken);

        return packets is null
            ? answers.Refused(request, EngineIoRefusal.BadRequest)
            : answers.WithBody(request, HttpStatus.Ok, PollContentType, EngineIoPollingPayload.Join(packets));
    }

    private async Task<HttpAnswer> PostAsync(HttpRequest request, SocketIoSession session, CancellationToken cancellationToken)
    {
        if (!session.TryBeginPost())
        {
            return answers.Refused(request, EngineIoRefusal.BadRequest);
        }

        try
        {
            var (packets, refusal) = await ReadPacketsAsync(request, cancellationToken);
            if (refusal is not null)
            {
                session.Decide(refusal);

                return AnswerEnd(request, refusal);
            }

            foreach (var packet in packets!)
            {
                if (await session.AcceptPacketAsync(packet, cancellationToken) is { } end)
                {
                    session.Decide(end);

                    return AnswerEnd(request, end);
                }
            }

            return answers.WithBody(request, HttpStatus.Ok, PostContentType, PostAnswer);
        }
        finally
        {
            session.EndPost();
        }
    }

    // A client close is answered ok; a body past maxPayload 413; anything else code 3.
    private HttpAnswer AnswerEnd(HttpRequest request, SocketIoSessionEnd end) => end.CloseCode switch
    {
        SocketIoSessionEnd.NormalClosure => answers.WithBody(request, HttpStatus.Ok, PostContentType, PostAnswer),
        SocketIoSessionEnd.MessageTooBig => answers.Empty(request, HttpStatus.ContentTooLarge),
        _ => answers.Refused(request, EngineIoRefusal.BadRequest),
    };

    private async Task<(List<EngineIoPacket>? Packets, SocketIoSessionEnd? Refusal)> ReadPacketsAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var (body, refusal) = await ReadBodyAsync(request.Body, cancellationToken);
        if (refusal is not null)
        {
            return (null, refusal);
        }

        if (IsBinaryBody(request))
        {
            return ([EngineIoPacket.Binary(body!)], null);
        }

        var packets = new List<EngineIoPacket>();

        return EngineIoPollingPayload.TrySplit(body, packets)
            ? (packets, null)
            : (null, SocketIoSessionEnd.ProtocolErrorClose("a polling body is not Engine.IO packets"));
    }

    private async Task<(byte[]? Body, SocketIoSessionEnd? Refusal)> ReadBodyAsync(IHttpRequestBody body, CancellationToken cancellationToken)
    {
        var limit = context.Limits.MaxMessageBytes;
        if (limit != 0 && body.AnnouncedLength > limit)
        {
            return (null, TooLarge());
        }

        if (!body.IsAnnounced)
        {
            return ([], null);
        }

        if (!body.IsReadable)
        {
            return (null, SocketIoSessionEnd.ProtocolErrorClose("a polling body cannot be read"));
        }

        using var buffer = new SocketIoPollingBodyBuffer(limit);
        var outcome = await body.CopyToAsync(buffer, cancellationToken);
        if (buffer.IsPastLimit || outcome == HttpRequestBodyDiscardOutcome.TooLarge)
        {
            return (null, TooLarge());
        }

        return outcome == HttpRequestBodyDiscardOutcome.Discarded
            ? (buffer.ToArray(), null)
            : (null, SocketIoSessionEnd.ProtocolErrorClose("a polling body was not read whole"));
    }

    private static SocketIoSessionEnd TooLarge() =>
        SocketIoSessionEnd.WebSocketClose(SocketIoSessionEnd.MessageTooBig, "a polling body is past --max-message");
}
