using System.Collections.Concurrent;
using System.Security.Cryptography;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// What every connection one <see cref="Mqtt5Broker"/> answers shares (ADR-0093 decision 0): the
/// client identifiers connected now, the Wills waiting out their delay, and the stream of
/// Application Messages the broker publishes. One instance is owned by the broker; never a static.
/// </summary>
/// <remarks>
/// <para>
/// <b>Client identifiers.</b> Each accepted connection registers its identifier, and an
/// identifier the broker assigns (section 3.1.3.1) is <c>surl-</c> followed by 32 lower-case hex
/// digits of 16 bytes from <see cref="RandomNumberGenerator"/>, drawn again until it names no
/// connected client (<c>[MQTT-3.2.2-16]</c>).
/// </para>
/// <para>
/// <b>Wills.</b> A Will whose publication is delayed (section 3.1.2.5) waits on a timer of the
/// connection's <see cref="TimeProvider"/>. A later <c>CONNECT</c> for the same identifier settles
/// it: with Clean Start 1 the old session ends and the Will is published at once; with Clean
/// Start 0 the session continues and the Will is discarded (<c>[MQTT-3.1.3-9]</c>).
/// </para>
/// <para>
/// <b>Publishing.</b> Every message the broker publishes goes to the one
/// <see cref="Surl.MessageBus.MessageBus"/> composed per <c>surl</c> run (ADR-0100 decision 3.9),
/// for the account of the client that sent it, so its routing, retained messages and topic
/// access are the bus's and every other messaging server sees it: a Will through
/// <see cref="Publish"/>, and a client's <c>PUBLISH</c> through its connection's own
/// <see cref="Surl.MessageBus.BusConnection.Publish"/>, which No Local needs.
/// </para>
/// </remarks>
public sealed class Mqtt5BrokerState
{
    private const string AssignedClientIdentifierPrefix = "surl-";
    private const int AssignedClientIdentifierRandomBytes = 16;

    private readonly Func<string> assignedClientIdentifierSource;
    private readonly ConcurrentDictionary<string, object> connectedClients = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PendingWill> pendingWills = new(StringComparer.Ordinal);
    private readonly Lock pendingWillsGate = new();

    /// <summary>
    /// Creates the state of a broker with no client connected and no Will pending, which publishes
    /// on <paramref name="bus"/>.
    /// </summary>
    /// <param name="bus">The message bus the messaging servers of the run share.</param>
    public Mqtt5BrokerState(Surl.MessageBus.MessageBus bus)
        : this(bus, NewAssignedClientIdentifier)
    {
    }

    /// <summary>
    /// Creates the state of a broker whose assigned client identifiers come from
    /// <paramref name="assignedClientIdentifierSource"/>.
    /// </summary>
    /// <param name="bus">The message bus the messaging servers of the run share.</param>
    /// <param name="assignedClientIdentifierSource">Draws one candidate identifier per call.</param>
    internal Mqtt5BrokerState(Surl.MessageBus.MessageBus bus, Func<string> assignedClientIdentifierSource)
    {
        ArgumentNullException.ThrowIfNull(bus);
        Bus = bus;
        this.assignedClientIdentifierSource = assignedClientIdentifierSource;
    }

    /// <summary>
    /// The message bus every message the broker publishes goes to.
    /// </summary>
    internal Surl.MessageBus.MessageBus Bus { get; }

    /// <summary>
    /// A fresh <c>surl-</c> identifier: 32 lower-case hex digits of 16 random bytes.
    /// </summary>
    /// <returns>The identifier.</returns>
    internal static string NewAssignedClientIdentifier() =>
        AssignedClientIdentifierPrefix + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(AssignedClientIdentifierRandomBytes));

    /// <summary>
    /// Registers a connection for the identifier its client sent.
    /// </summary>
    /// <param name="clientIdentifier">The client's identifier.</param>
    /// <returns>The registration, which <see cref="UnregisterClient"/> takes back.</returns>
    internal object RegisterClient(string clientIdentifier)
    {
        var registration = new object();
        connectedClients[clientIdentifier] = registration;
        return registration;
    }

    /// <summary>
    /// Assigns an identifier no connected client has and registers a connection for it.
    /// </summary>
    /// <param name="registration">The registration, which <see cref="UnregisterClient"/> takes back.</param>
    /// <returns>The assigned identifier.</returns>
    internal string RegisterAssignedClient(out object registration)
    {
        registration = new object();
        string clientIdentifier;
        do
        {
            clientIdentifier = assignedClientIdentifierSource();
        }
        while (!connectedClients.TryAdd(clientIdentifier, registration));

        return clientIdentifier;
    }

    /// <summary>
    /// Whether a connection is registered for <paramref name="clientIdentifier"/>.
    /// </summary>
    /// <param name="clientIdentifier">The identifier.</param>
    /// <returns><see langword="true"/> when one is.</returns>
    internal bool IsConnected(string clientIdentifier) => connectedClients.ContainsKey(clientIdentifier);

    /// <summary>
    /// Ends a connection's registration, unless a later connection has registered the same identifier since.
    /// </summary>
    /// <param name="clientIdentifier">The identifier it registered.</param>
    /// <param name="registration">What registering it returned.</param>
    internal void UnregisterClient(string clientIdentifier, object registration) =>
        connectedClients.TryRemove(new KeyValuePair<string, object>(clientIdentifier, registration));

    /// <summary>
    /// Publishes <paramref name="message"/> on <see cref="Bus"/> for <paramref name="account"/>.
    /// </summary>
    /// <param name="message">The message, its topic canonical.</param>
    /// <param name="account">The publishing client's account; <see langword="null"/> for an anonymous one.</param>
    /// <param name="now">When it is published; its Message Expiry Interval counts from here.</param>
    /// <returns>What the bus did with it.</returns>
    internal BusPublishResult Publish(Mqtt5ApplicationMessage message, string? account, DateTimeOffset now) =>
        Bus.Publish(Mqtt5BusMessages.ToBusMessage(message, now), account);

    /// <summary>
    /// Publishes a Will after <paramref name="delay"/>, unless a <c>CONNECT</c> for
    /// <paramref name="clientIdentifier"/> settles it first. A Will already pending for the
    /// identifier belonged to a session that has ended, so it is published at once.
    /// </summary>
    /// <param name="clientIdentifier">The identifier of the client whose Will it is.</param>
    /// <param name="will">The message the Will stands for, its topic canonical.</param>
    /// <param name="account">The account of the client whose Will it is; <see langword="null"/> for an anonymous one.</param>
    /// <param name="delay">How long to wait; more than zero.</param>
    /// <param name="timeProvider">The clock the delay is measured on.</param>
    internal void ScheduleWill(string clientIdentifier, Mqtt5ApplicationMessage will, string? account, TimeSpan delay, TimeProvider timeProvider)
    {
        var pending = new PendingWill(will, account, timeProvider);
        PendingWill? replaced;
        lock (pendingWillsGate)
        {
            pendingWills.Remove(clientIdentifier, out replaced);
            pendingWills[clientIdentifier] = pending;
            pending.Timer = timeProvider.CreateTimer(_ => PublishWhenDue(clientIdentifier, pending), null, delay, Timeout.InfiniteTimeSpan);
        }

        if (replaced is not null)
        {
            replaced.Timer!.Dispose();
            PublishWill(replaced);
        }
    }

    /// <summary>
    /// Settles the Will pending for <paramref name="clientIdentifier"/>, if any, as a new
    /// <c>CONNECT</c> for it requires: published at once when <paramref name="cleanStart"/> ends
    /// the old session, discarded when the session continues.
    /// </summary>
    /// <param name="clientIdentifier">The identifier of the connecting client.</param>
    /// <param name="cleanStart">The new <c>CONNECT</c>'s Clean Start flag.</param>
    internal void SettlePendingWill(string clientIdentifier, bool cleanStart)
    {
        PendingWill? pending;
        lock (pendingWillsGate)
        {
            pendingWills.Remove(clientIdentifier, out pending);
        }

        if (pending is null)
        {
            return;
        }

        pending.Timer!.Dispose();
        if (cleanStart)
        {
            PublishWill(pending);
        }
    }

    // A timer can fire while a CONNECT is settling its Will; whichever removes it first owns it.
    private void PublishWhenDue(string clientIdentifier, PendingWill pending)
    {
        lock (pendingWillsGate)
        {
            if (!((ICollection<KeyValuePair<string, PendingWill>>)pendingWills).Remove(new(clientIdentifier, pending)))
            {
                return;
            }
        }

        pending.Timer!.Dispose();
        PublishWill(pending);
    }

    private void PublishWill(PendingWill pending) => Publish(pending.Will, pending.Account, pending.TimeProvider.GetUtcNow());

    private sealed class PendingWill(Mqtt5ApplicationMessage will, string? account, TimeProvider timeProvider)
    {
        public Mqtt5ApplicationMessage Will { get; } = will;

        public string? Account { get; } = account;

        public TimeProvider TimeProvider { get; } = timeProvider;

        public ITimer? Timer { get; set; }
    }
}
