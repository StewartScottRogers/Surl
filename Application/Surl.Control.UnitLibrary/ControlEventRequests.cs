using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Control;

/// <summary>
/// Answers <c>GET /v1/events</c>, <c>GET /v1/events/wait</c> and <c>GET /v1/exchanges</c> from the endpoint's own
/// event log (ADR-0124 decision 7).
/// </summary>
/// <param name="events">The endpoint's event log.</param>
/// <param name="answers">Writes the answers.</param>
/// <param name="timeProvider">The clock a wait's bound runs on.</param>
internal sealed class ControlEventRequests(ControlEventLog events, ControlAnswers answers, TimeProvider timeProvider)
{
    /// <summary>The query keys <c>GET /v1/events</c> takes.</summary>
    public static readonly string[] EventsQueryKeys = ["after", "kind", "scheme", "listen_url", "account", "limit"];

    /// <summary>The query keys <c>GET /v1/events/wait</c> takes.</summary>
    public static readonly string[] WaitQueryKeys = ["after", "kind", "scheme", "listen_url", "account", "timeout_ms"];

    /// <summary>The query keys <c>GET /v1/exchanges</c> takes.</summary>
    public static readonly string[] ExchangesQueryKeys = ["after_exchange_id", "scheme", "listen_url", "limit"];

    /// <summary>The longest a wait may be asked to wait, in milliseconds.</summary>
    public const long MaxWaitMilliseconds = 60000;

    /// <summary>Lists the held events a query asks for, oldest first, at most <c>limit</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>{"events", "has_more", "dropped_event_count"}</c>.</returns>
    public Task<HttpAnswer> FindEventsAsync(ControlRequest request)
    {
        var query = EventQuery(request.Query);
        var limit = (int)request.Query.Number("limit", 1, 1000, 100);
        var view = events.View();
        var exchanges = view.Exchanges();
        var matching = view.Events.Where(surlEvent => query.Matches(surlEvent, exchanges)).Take(limit + 1).ToArray();
        var answer = new ControlEventsJson(
            [.. matching.Take(limit).Select(ControlEventWriter.ToJson)],
            matching.Length > limit,
            view.DroppedEventCount);

        return Task.FromResult(answers.Json(request.Http, HttpStatus.Ok, answer, ControlJsonContext.Default.ControlEventsJson));
    }

    /// <summary>
    /// Answers the first held or next added event a query asks for, or <c>timed_out</c> once <c>timeout_ms</c> has
    /// passed on the injected clock.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns><c>{"event": {...}}</c> or <c>{"timed_out": true}</c>.</returns>
    public async Task<HttpAnswer> WaitForEventAsync(ControlRequest request)
    {
        var query = EventQuery(request.Query);
        var timeout = TimeSpan.FromMilliseconds(request.Query.Number("timeout_ms", 1, MaxWaitMilliseconds, 10000));
        using var deadline = new CancellationTokenSource(timeout, timeProvider);
        using var deadlineOrExchange = CancellationTokenSource.CreateLinkedTokenSource(request.CancellationToken, deadline.Token);
        while (true)
        {
            var view = events.View();
            var exchanges = view.Exchanges();
            var found = view.Events.FirstOrDefault(surlEvent => query.Matches(surlEvent, exchanges));
            if (found is not null)
            {
                return Wait(request, new ControlEventWaitJson(ControlEventWriter.ToJson(found), null));
            }

            try
            {
                await view.NextEventAdded.WaitAsync(deadlineOrExchange.Token);
            }
            catch (OperationCanceledException) when (!request.CancellationToken.IsCancellationRequested)
            {
                return Wait(request, new ControlEventWaitJson(null, true));
            }
        }
    }

    /// <summary>Lists the held exchanges a query asks for, in <c>get_exchanges</c>'s shape.</summary>
    /// <param name="request">The request.</param>
    /// <returns><c>{"exchanges", "dropped_exchange_count", "has_more"}</c>.</returns>
    public Task<HttpAnswer> FindExchangesAsync(ControlRequest request)
    {
        var answer = ControlExchangeWriter.Write(
            events.View(),
            request.Query.Number("after_exchange_id", 0, long.MaxValue, 0),
            request.Query.Text("scheme"),
            request.Query.Text("listen_url"),
            (int)request.Query.Number("limit", 1, 100, 20));

        return Task.FromResult(answers.Json(request.Http, HttpStatus.Ok, answer, ControlJsonContext.Default.ControlExchangesJson));
    }

    private static ControlEventQuery EventQuery(ControlQuery query)
    {
        var kind = query.Text("kind");
        if (kind is not null && !ControlEventKinds.Known.Contains(kind))
        {
            throw new ControlErrorException(ControlError.BadRequest(
                $"query key kind must be one of {string.Join(", ", ControlEventKinds.Known.Order(StringComparer.Ordinal))}"));
        }

        return new ControlEventQuery(
            query.Number("after", 0, long.MaxValue, 0),
            kind,
            query.Text("scheme"),
            query.Text("listen_url"),
            query.Text("account"));
    }

    private HttpAnswer Wait(ControlRequest request, ControlEventWaitJson answer) =>
        answers.Json(request.Http, HttpStatus.Ok, answer, ControlJsonContext.Default.ControlEventWaitJson);
}
