using System.Globalization;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The requests of the Server-Sent Events and Long Polling transports and of HTTP Post, which a hub's
/// endpoint base answers when the request is not a WebSocket upgrade (TransportProtocols.md "HTTP Post",
/// "Server-Sent Events" and "Long Polling"; ADR-0119, decision 2, amendment 1).
/// </summary>
/// <remarks>
/// Every one names its connection by <c>id</c>: none is <c>400</c>, an unknown or ended one <c>404</c>, one
/// another caller negotiated <c>403</c>, and one on a WebSocket <c>400</c>. A <c>GET</c> with
/// <c>Accept: text/event-stream</c> takes the connection up and is answered <c>200</c> as an open stream of
/// events; any other <c>GET</c> is a poll, the first answered <c>200</c> with no content at once and each
/// later one with the records waiting, <c>200</c> with no content after 90 s, or <c>204</c> when a newer poll
/// superseded it or the connection ended. A <c>POST</c> is <c>200</c> once its payload is processed, <c>409</c>
/// while an earlier post is unprocessed, <c>413</c> past <c>--max-message</c>; a <c>DELETE</c> is <c>202</c>.
/// </remarks>
internal sealed partial class SignalRRequestAnswerer
{
    private static readonly HttpStatus Accepted = new(202, "Accepted");

    private static readonly HttpStatus NoContent = new(204, "No Content");

    private static bool AcceptsEventStream(HttpRequest request) =>
        request.Fields.Any(field => field.Key.Equals("Accept", StringComparison.OrdinalIgnoreCase)
            && field.Value.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase));

    private async Task<HttpAnswer> AnswerHttpTransportAsync(HttpRequest request, SignalRRequestTarget target, string? accountName, CancellationToken cancellationToken)
    {
        if (request.Method is not ("GET" or "POST" or "DELETE"))
        {
            return Answer(request, HttpStatus.MethodNotAllowed, [new("Allow", "GET, POST, DELETE")]);
        }

        if (target.QueryParameter("id") is not { Length: > 0 } id)
        {
            return Answer(request, HttpStatus.BadRequest);
        }

        if (request.Method == "POST")
        {
            return await AnswerPostAsync(request, id, accountName, cancellationToken);
        }

        if (request.Method == "DELETE")
        {
            return AnswerDelete(request, id, accountName);
        }

        return AcceptsEventStream(request)
            ? AnswerServerSentEvents(request, id, accountName)
            : await AnswerPollAsync(request, id, accountName, cancellationToken);
    }

    // The transport state of a connection already taken up, or the refusal to answer with.
    private HttpAnswer? FindTransport(HttpRequest request, string id, string? accountName, out SignalRHttpTransportState? state)
    {
        state = null;
        var outcome = connections.FindTakenUp(id, accountName, out var hubConnection);
        if (outcome != SignalRUpgradeOutcome.TakenUp)
        {
            return Answer(request, UpgradeRefusalStatus(outcome));
        }

        state = hubConnection!.HttpTransport;

        return state is null ? Answer(request, HttpStatus.BadRequest) : null;
    }

    private async Task<HttpAnswer> AnswerPostAsync(HttpRequest request, string id, string? accountName, CancellationToken cancellationToken)
    {
        if (FindTransport(request, id, accountName, out var state) is { } refusal)
        {
            return refusal;
        }

        var (payload, bodyRefusal) = await ReadPostBodyAsync(request, cancellationToken);
        if (bodyRefusal is not null)
        {
            return bodyRefusal;
        }

        var processed = state!.Posts.Post(payload!);
        if (processed is null)
        {
            context.Log.Note("HTTP Post refused: an earlier post is still outstanding.");

            return Answer(request, Conflict);
        }

        return await processed ? Answer(request, HttpStatus.Ok) : Answer(request, HttpStatus.NotFound);
    }

    private async Task<(byte[]? Payload, HttpAnswer? Refusal)> ReadPostBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var limit = context.Limits.MaxMessageBytes;
        if (!request.Body.IsAnnounced)
        {
            return ([], null);
        }

        if (!request.Body.IsReadable)
        {
            return (null, BodyRefusal(HttpStatus.BadRequest));
        }

        if (limit != 0 && request.Body.AnnouncedLength > limit)
        {
            return (null, BodyRefusal(HttpStatus.ContentTooLarge));
        }

        using var buffer = new MemoryStream();
        var outcome = await request.Body.CopyToAsync(buffer, cancellationToken);
        if (outcome == HttpRequestBodyDiscardOutcome.Discarded && (limit == 0 || buffer.Length <= limit))
        {
            return (buffer.ToArray(), null);
        }

        var status = outcome == HttpRequestBodyDiscardOutcome.Incomplete ? HttpStatus.BadRequest : HttpStatus.ContentTooLarge;

        return (null, BodyRefusal(status));
    }

    private HttpAnswer BodyRefusal(HttpStatus status) =>
        new(status, [.. CommonFields(0)], HttpAnswerBody.None, IsRefusal: true, LeavesRequestBodyUnread: true);

    private HttpAnswer AnswerDelete(HttpRequest request, string id, string? accountName)
    {
        if (FindTransport(request, id, accountName, out var state) is { } refusal)
        {
            return refusal;
        }

        if (state!.LongPolling is { } longPolling)
        {
            longPolling.Delete();
        }
        else
        {
            state.Posts.Close(SignalRSessionEnd.Silent("the client sent DELETE"));
        }

        return Answer(request, Accepted);
    }

    private HttpAnswer AnswerServerSentEvents(HttpRequest request, string id, string? accountName)
    {
        var outcome = connections.TakeUp(id, accountName, out var hubConnection);
        if (outcome != SignalRUpgradeOutcome.TakenUp)
        {
            return Answer(request, UpgradeRefusalStatus(outcome));
        }

        var posts = new SignalRPostedPayloads();
        hubConnection!.HttpTransport = new SignalRHttpTransportState(posts, null);

        return new HttpAnswer(
            HttpStatus.Ok,
            [
                new("Date", context.TimeProvider.GetUtcNow().ToString("r", CultureInfo.InvariantCulture)),
                new("Content-Type", "text/event-stream"),
                new("Cache-Control", "no-cache"),
            ],
            new HttpAnswerBody.Streamed((writer, token) => RunServerSentEventsAsync(hubConnection, posts, writer, token)),
            IsRefusal: false,
            LeavesRequestBodyUnread: request.Body.IsAnnounced);
    }

    // The open response is the session's: it ends when the session does, and the id is then forgotten.
    private async Task RunServerSentEventsAsync(SignalRNegotiatedConnection hubConnection, SignalRPostedPayloads posts, HttpAnswerChunkWriter writer, CancellationToken token)
    {
        try
        {
            using var session = new SignalRHubSession(
                new SignalRServerSentEventsTransport(posts, writer), context, FaultsFor(hubConnection));

            // An event stream's comment line: it makes the client see the stream open before any record.
            await writer(":\r\n\r\n"u8.ToArray(), token);
            await session.RunAsync();
        }
        finally
        {
            connections.End(hubConnection);
        }
    }

    private SignalRInvocationFaults FaultsFor(SignalRNegotiatedConnection hubConnection) =>
        new(context, hubPath.EndpointBase, hubConnection.AccountName);

    private async Task<HttpAnswer> AnswerPollAsync(HttpRequest request, string id, string? accountName, CancellationToken cancellationToken)
    {
        var found = connections.FindTakenUp(id, accountName, out var hubConnection);
        if (found == SignalRUpgradeOutcome.NotFound)
        {
            return AnswerFirstPoll(request, id, accountName);
        }

        if (found != SignalRUpgradeOutcome.TakenUp)
        {
            return Answer(request, UpgradeRefusalStatus(found));
        }

        if (hubConnection!.HttpTransport?.LongPolling is not { } longPolling)
        {
            return Answer(request, HttpStatus.BadRequest);
        }

        return PollAnswer(request, await longPolling.PollAsync(cancellationToken));
    }

    // The first poll takes the connection up and is answered at once; the session then runs on without a request.
    private HttpAnswer AnswerFirstPoll(HttpRequest request, string id, string? accountName)
    {
        var outcome = connections.TakeUp(id, accountName, out var hubConnection);
        if (outcome != SignalRUpgradeOutcome.TakenUp)
        {
            return Answer(request, UpgradeRefusalStatus(outcome));
        }

        var posts = new SignalRPostedPayloads();
        var transport = new SignalRLongPollingTransport(posts, context.TimeProvider);
        hubConnection!.HttpTransport = new SignalRHttpTransportState(posts, transport);
        _ = RunLongPollingAsync(hubConnection, transport);

        return PollAnswer(request, new SignalRPollResult(200, []));
    }

    // The session outlives the request that started it, so it runs on the engine's shutdown alone.
    private async Task RunLongPollingAsync(SignalRNegotiatedConnection hubConnection, SignalRLongPollingTransport transport)
    {
        try
        {
            using var session = new SignalRHubSession(
                transport, context with { CancellationToken = context.ShutdownToken }, FaultsFor(hubConnection));
            await session.RunAsync();
        }
        finally
        {
            connections.End(hubConnection);
        }
    }

    private HttpAnswer PollAnswer(HttpRequest request, SignalRPollResult result)
    {
        if (result.Status == 204)
        {
            return new HttpAnswer(
                NoContent,
                [new("Date", context.TimeProvider.GetUtcNow().ToString("r", CultureInfo.InvariantCulture))],
                HttpAnswerBody.None,
                IsRefusal: false,
                LeavesRequestBodyUnread: request.Body.IsAnnounced);
        }

        return new HttpAnswer(
            HttpStatus.Ok,
            [.. CommonFields(result.Body.Length), new("Content-Type", "application/octet-stream")],
            new HttpAnswerBody.Bytes(result.Body),
            IsRefusal: false,
            LeavesRequestBodyUnread: request.Body.IsAnnounced);
    }
}
