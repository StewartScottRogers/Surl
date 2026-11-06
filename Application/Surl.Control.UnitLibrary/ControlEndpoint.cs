using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Control;

/// <summary>
/// The loopback control endpoint (ADR-0124 decision 7): HTTP and JSON under <c>/v1/</c> that let a test suite not
/// written in .NET read a running instance's events and exchanges, change its fault plan, and seed, reset, snapshot
/// and restore its stores, through an <see cref="IControlTarget"/>.
/// </summary>
/// <remarks>
/// One endpoint serves one instance. <see cref="FeedEventsAsync"/> keeps its event log filled from the target and
/// runs for the instance's life; <see cref="ServeAsync"/> answers each connection the control listener accepts.
/// Every request needs the bearer token and a loopback peer; bodies are at most <see cref="MaxBodyBytes"/>. No answer
/// holds the token or a password.
/// </remarks>
public sealed class ControlEndpoint
{
    /// <summary>The largest request body read: 16 MiB.</summary>
    public const long MaxBodyBytes = 16L * 1024 * 1024;

    /// <summary>
    /// How long a request without the token waits before its <c>401</c>: the same fixed delay as every other refused
    /// login (<c>AuthenticationPolicy.RefusalDelay</c>).
    /// </summary>
    public static readonly TimeSpan RefusalDelay = TimeSpan.FromSeconds(1);

    private readonly IControlTarget target;
    private readonly ControlToken token;
    private readonly ControlEventLog events;
    private readonly ControlSnapshotShelf snapshots = new();

    /// <summary>Creates the endpoint for one instance.</summary>
    /// <param name="target">What the endpoint drives.</param>
    /// <param name="token">The token every request must carry.</param>
    public ControlEndpoint(IControlTarget target, ControlToken token)
        : this(target, token, new ControlEventLog())
    {
    }

    /// <summary>Creates the endpoint with an event log of chosen bounds, for tests.</summary>
    /// <param name="target">What the endpoint drives.</param>
    /// <param name="token">The token every request must carry.</param>
    /// <param name="events">The event log.</param>
    internal ControlEndpoint(IControlTarget target, ControlToken token, ControlEventLog events)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(token);
        this.target = target;
        this.token = token;
        this.events = events;
    }

    /// <summary>
    /// Reads the target's events into the endpoint's log - the last 10000 events and at most 64 MiB of exchange
    /// bytes - until <paramref name="cancellationToken"/> is cancelled, then returns.
    /// </summary>
    /// <param name="cancellationToken">Ends the feed.</param>
    /// <returns>The feed.</returns>
    public async Task FeedEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var surlEvent in target.ReadEventsAsync(cancellationToken).WithCancellation(cancellationToken))
            {
                events.Add(surlEvent);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The instance is stopping; the feed ends with it.
        }
    }

    /// <summary>Answers the control requests of one connection until it closes.</summary>
    /// <param name="connection">The connection the control listener accepted.</param>
    /// <param name="context">The exchange's context; its upload limit is replaced by <see cref="MaxBodyBytes"/>.</param>
    /// <returns>The serving.</returns>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);
        var answers = new ControlAnswers(context.TimeProvider);
        var handler = new ControlRequestHandler(Routes(answers, context.TimeProvider), token, context.RemoteEndPoint, answers, context.TimeProvider);
        var bounded = context with { Limits = context.Limits with { MaxUploadBytes = MaxBodyBytes } };

        return new Http1ConnectionServer(handler, bounded).ServeAsync(connection);
    }

    private ControlRoute[] Routes(ControlAnswers answers, TimeProvider timeProvider)
    {
        var eventRequests = new ControlEventRequests(events, answers, timeProvider);
        var storeRequests = new ControlStoreRequests(target, snapshots, answers);

        return
        [
            new("GET", ["events"], ControlEventRequests.EventsQueryKeys, eventRequests.FindEventsAsync),
            new("GET", ["events", "wait"], ControlEventRequests.WaitQueryKeys, eventRequests.WaitForEventAsync),
            new("GET", ["exchanges"], ControlEventRequests.ExchangesQueryKeys, eventRequests.FindExchangesAsync),
            new("POST", ["faults"], [], storeRequests.AddFaultAsync),
            new("GET", ["faults"], [], storeRequests.ListFaultsAsync),
            new("DELETE", ["faults", "{}"], [], storeRequests.RemoveFaultAsync),
            new("POST", ["mail"], ["to"], storeRequests.SeedMailAsync),
            new("PUT", ["files", "{*}"], [], storeRequests.WriteFileAsync),
            new("DELETE", ["files", "{*}"], [], storeRequests.DeleteFileAsync),
            new("POST", ["messages"], [], storeRequests.PublishMessageAsync),
            new("POST", ["accounts"], [], storeRequests.AddAccountAsync),
            new("DELETE", ["accounts", "{}"], [], storeRequests.RemoveAccountAsync),
            new("POST", ["reset"], [], storeRequests.ResetAsync),
            new("POST", ["snapshots"], [], storeRequests.SnapshotAsync),
            new("POST", ["snapshots", "{}", "restore"], [], storeRequests.RestoreAsync),
        ];
    }
}
