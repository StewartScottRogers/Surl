using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// One event callback (ADR-0124, decision 5): its own subscription to the instance's event stream, at the default
/// capacity and without bytes, and its own pump task that runs the callback for each event the query matches, off
/// the serving path, so a slow callback loses events as any slow subscriber does and never blocks a server. A
/// callback that throws is published as a <see cref="HookFailedEvent"/> and the pump goes on to the next event.
/// Disposing it stops delivery: no callback starts afterwards, and the running one's token is cancelled.
/// </summary>
internal sealed class EventCallbackHook : IDisposable
{
    private readonly string hookId;
    private readonly SurlEventQuery query;
    private readonly Func<SurlEvent, CancellationToken, ValueTask> callback;
    private readonly SurlEventStream stream;
    private readonly SurlEventSubscription subscription;
    private readonly CancellationTokenSource stopped = new();
    private readonly Dictionary<long, ExchangeFacts> openExchanges = [];

    /// <summary>Subscribes and starts the pump.</summary>
    /// <param name="hookId">The callback's identifier, <c>h&lt;n&gt;</c>, named in a <see cref="HookFailedEvent"/>.</param>
    /// <param name="query">Which events the callback runs for.</param>
    /// <param name="callback">The test's callback.</param>
    /// <param name="stream">The instance's event stream.</param>
    public EventCallbackHook(
        string hookId, SurlEventQuery query, Func<SurlEvent, CancellationToken, ValueTask> callback, SurlEventStream stream)
    {
        this.hookId = hookId;
        this.query = query;
        this.callback = callback;
        this.stream = stream;
        subscription = stream.Subscribe(new SurlEventSubscriptionOptions());
        Pump = Task.Run(PumpAsync);
    }

    /// <summary>The pump; it ends once the hook is disposed and the buffered events are passed over.</summary>
    internal Task Pump { get; }

    /// <summary>Stops delivery and cancels the running callback's token; disposing again does nothing more.</summary>
    public void Dispose()
    {
        if (!stopped.IsCancellationRequested)
        {
            stopped.Cancel();
            _ = subscription.DisposeAsync();
        }
    }

    private async Task PumpAsync()
    {
        await foreach (var surlEvent in subscription.ReadAllAsync(CancellationToken.None))
        {
            if (!stopped.IsCancellationRequested && Matches(surlEvent))
            {
                await RunCallbackAsync(surlEvent);
            }
        }
    }

    // Keeps the facts of each exchange open since the hook subscribed, so a query by scheme, listen URL or account
    // matches an event of that exchange as FindEvents would.
    private bool Matches(SurlEvent surlEvent)
    {
        if (surlEvent is ExchangeStartedEvent started)
        {
            openExchanges[started.ExchangeId] = new ExchangeFacts(started);
        }

        var exchangeId = SurlEventFacts.ExchangeIdOf(surlEvent);
        var exchange = exchangeId is { } id ? openExchanges.GetValueOrDefault(id) : null;
        exchange?.Note(surlEvent);
        if (surlEvent is ExchangeEndedEvent ended)
        {
            openExchanges.Remove(ended.ExchangeId);
        }

        return SurlEventFacts.Matches(surlEvent, exchange, query);
    }

    private async Task RunCallbackAsync(SurlEvent surlEvent)
    {
        try
        {
            await callback(surlEvent, stopped.Token);
        }
        catch (Exception exception)
        {
            PublishFailure(surlEvent, exception);
        }
    }

    // Not when the callback ended because the hook was disposed, nor when it threw on its own failure event, so a
    // callback that always throws cannot feed itself.
    private void PublishFailure(SurlEvent surlEvent, Exception exception)
    {
        if (!stopped.IsCancellationRequested && !(surlEvent is HookFailedEvent failed && failed.HookId == hookId))
        {
            stream.Publish(new HookFailedEvent(hookId, HookFailure.Threw, exception.Message));
        }
    }
}
