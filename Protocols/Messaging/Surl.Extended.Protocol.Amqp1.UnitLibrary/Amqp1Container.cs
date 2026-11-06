using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The AMQP 1.0 container (ADR-0107): one <see cref="IConnectionProtocolServer"/> that serves a whole
/// <c>amqp1</c> connection - its protocol headers and SASL (<see cref="Amqp1SecurityNegotiation"/>),
/// then its connection, sessions and links (<see cref="Amqp1ContainerConnection"/>) for the account
/// SASL logged in, its nodes the shared message bus's.
/// </summary>
/// <remarks>
/// <para>
/// <b>The head timeout</b> (ADR-0107 decision 7) runs from accept until the client's <c>open</c> is
/// read, the headers, a TLS upgrade and SASL included; when it runs out the connection is closed with
/// no more bytes.
/// </para>
/// <para>
/// <b><c>--max-time</c> and the connection limits</b> are the engine's. Cancelled for a limit once
/// <c>open</c> has been exchanged, the container writes <c>close</c> with
/// <c>amqp:resource-limit-exceeded</c>; before it, nothing more. A connection past
/// <c>--max-connections</c> or <c>--max-connections-per-address</c> is refused by the engine before
/// any byte is read, so the container, which writes no refusal, sends it no bytes.
/// </para>
/// </remarks>
public sealed class Amqp1Container : IConnectionProtocolServer
{
    // Timers take at most uint.MaxValue - 1 milliseconds; a longer head timeout is no limit at all.
    private static readonly TimeSpan MaxTimerDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1.0);

    private readonly ISaslAuthenticationPolicy policy;
    private readonly MessageBus.MessageBus bus;
    private readonly Amqp1ContainerOptions containerOptions;
    private readonly Amqp1SecurityNegotiationOptions negotiationOptions;
    private readonly Func<BusConnection, IAmqp1NodeDirectory> nodesFor;

    /// <summary>Creates the container over <paramref name="bus"/>, whose logins <paramref name="policy"/> judges.</summary>
    /// <param name="policy">Offers the SASL mechanisms and runs each login but <c>ANONYMOUS</c>.</param>
    /// <param name="bus">The process's shared message bus: its topics and queues are the container's nodes.</param>
    /// <param name="containerOptions">The container id and the idle timeout; <see cref="Amqp1ContainerOptions.Default"/> when omitted.</param>
    /// <param name="negotiationOptions">Whether anonymous logins and the TLS header are allowed; <see cref="Amqp1SecurityNegotiationOptions.Default"/> when omitted.</param>
    public Amqp1Container(
        ISaslAuthenticationPolicy policy,
        MessageBus.MessageBus bus,
        Amqp1ContainerOptions? containerOptions = null,
        Amqp1SecurityNegotiationOptions? negotiationOptions = null)
        : this(policy, bus, containerOptions, negotiationOptions, null)
    {
    }

    // The tests' seam: a node directory of their own for each connection.
    internal Amqp1Container(
        ISaslAuthenticationPolicy policy,
        MessageBus.MessageBus bus,
        Amqp1ContainerOptions? containerOptions,
        Amqp1SecurityNegotiationOptions? negotiationOptions,
        Func<BusConnection, IAmqp1NodeDirectory>? nodesFor)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(bus);

        this.policy = policy;
        this.bus = bus;
        this.containerOptions = containerOptions ?? Amqp1ContainerOptions.Default;
        this.negotiationOptions = negotiationOptions ?? Amqp1SecurityNegotiationOptions.Default;
        this.nodesFor = nodesFor ?? (busConnection => new Amqp1BusNodeDirectory(bus, busConnection));
    }

    /// <summary>
    /// The schemes answered: <c>amqp1</c>, and <c>amqp1s</c>, the same exchange behind implicit TLS,
    /// whose handshake the engine performs (ADR-0107 decision 7).
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["amqp1", "amqp1s"]);

    /// <summary>Answers <paramref name="connection"/> from its first header to its end.</summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        using var headTimeout = new CancellationTokenSource(TimerDelay(context.Limits.HeadTimeout), context.TimeProvider);
        var negotiation = await NegotiateWithinHeadTimeoutAsync(connection, context, headTimeout.Token);
        if (negotiation is { IsReadyForAmqp: true })
        {
            await new Amqp1ContainerConnection(connection, context, containerOptions, bus, negotiation.AccountName, nodesFor)
                .ServeAsync(headTimeout.Token);
        }
    }

    private static TimeSpan TimerDelay(TimeSpan headTimeout) => headTimeout > MaxTimerDelay ? Timeout.InfiniteTimeSpan : headTimeout;

    // The negotiation's end; null, with nothing more written, when the head timeout or a limit cut it off.
    private async ValueTask<Amqp1SecurityNegotiationResult?> NegotiateWithinHeadTimeoutAsync(
        IConnection connection, ExchangeContext context, CancellationToken headDeadline)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headDeadline);
        var negotiation = new Amqp1SecurityNegotiation(policy, negotiationOptions, connection, context with { CancellationToken = cancellation.Token });
        try
        {
            return await negotiation.NegotiateAsync();
        }
        catch (OperationCanceledException) when (!context.ShutdownToken.IsCancellationRequested)
        {
            context.Log.Note(context.CancellationToken.IsCancellationRequested
                ? "The exchange was cancelled for a limit before open; closed with no bytes."
                : "No open arrived within the head timeout; closed with no bytes.");
            return null;
        }
    }
}
