using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// What every connection one <see cref="Mqtt5Broker"/> answers shares (ADR-0093 decision 0): the
/// client identifiers connected now, the sessions kept between connections, the Wills waiting out
/// their delay, and the stream of Application Messages the broker publishes. One instance is owned
/// by the broker; never a static.
/// </summary>
/// <remarks>
/// <para>
/// <b>Client identifiers.</b> Each accepted connection registers its identifier, and an
/// identifier the broker assigns (section 3.1.3.1) is <c>surl-</c> followed by 32 lower-case hex
/// digits of 16 bytes from the exchange's <see cref="ExchangeContext.Identifiers"/> (ADR-0124
/// decision 6, so a seeded instance assigns the same identifiers every run), drawn again until it
/// names no connected client (<c>[MQTT-3.2.2-16]</c>).
/// </para>
/// <para>
/// <b>Wills.</b> A Will whose publication is delayed (section 3.1.2.5) waits on a timer of the
/// connection's <see cref="TimeProvider"/>. A later <c>CONNECT</c> for the same identifier settles
/// it: when the old session ended the Will is published at once; when the new connection resumed
/// the session it is discarded (<c>[MQTT-3.1.3-9]</c>).
/// </para>
/// <para>
/// <b>Sessions.</b> A session (<see cref="Mqtt5Session"/>) whose Session Expiry Interval is above
/// 0 is kept that long after its connection closes, on a timer of the connection's
/// <see cref="TimeProvider"/>, and resumed by a <c>CONNECT</c> with Clean Start 0 for the same
/// identifier and account. At most <see cref="MaxKeptSessions"/> are kept with no connection.
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
    private const int DefaultMaxKeptSessions = 1000;

    private readonly Dictionary<string, Mqtt5ClientRegistration> connectedClients = new(StringComparer.Ordinal);
    private readonly Lock connectedClientsGate = new();
    private readonly Dictionary<string, PendingWill> pendingWills = new(StringComparer.Ordinal);
    private readonly Lock pendingWillsGate = new();
    private readonly Dictionary<string, Mqtt5Session> sessions = new(StringComparer.Ordinal);
    private readonly Lock sessionsGate = new();

    /// <summary>
    /// Creates the state of a broker with no client connected and no Will pending, which publishes
    /// on <paramref name="bus"/>.
    /// </summary>
    /// <param name="bus">The message bus the messaging servers of the run share.</param>
    public Mqtt5BrokerState(Surl.MessageBus.MessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        Bus = bus;
    }

    /// <summary>
    /// The message bus every message the broker publishes goes to.
    /// </summary>
    internal Surl.MessageBus.MessageBus Bus { get; }

    /// <summary>
    /// The most sessions kept with no connection holding them (ADR-0093 decision 3); a new
    /// session past it is refused <c>CONNACK</c> <c>0x97</c>.
    /// </summary>
    internal int MaxKeptSessions { get; init; } = DefaultMaxKeptSessions;

    /// <summary>The most messages that wait for one session at once.</summary>
    internal int MaxQueuedMessages { get; init; } = Mqtt5Session.MaxQueuedMessages;

    /// <summary>The most payload bytes that wait for one session at once.</summary>
    internal long MaxQueuedPayloadBytes { get; init; } = Mqtt5Session.MaxQueuedPayloadBytes;

    /// <summary>
    /// Opens the session for an accepted <c>CONNECT</c> (section 3.1.2.4). A kept session with no
    /// connection is resumed when Clean Start is 0 and the login is for the session's account;
    /// otherwise it ends and a new one starts. A session another connection still holds is never
    /// resumed: the new one replaces it, and it ends with its connection. A takeover
    /// (<see cref="RegisterClient"/>) waits for the older connection to end first, so its session
    /// is no longer held by then.
    /// </summary>
    /// <param name="clientIdentifier">The client identifier.</param>
    /// <param name="account">The account the login matched; <see langword="null"/> for an anonymous one.</param>
    /// <param name="cleanStart">The <c>CONNECT</c>'s Clean Start flag.</param>
    /// <param name="expiry">The Session Expiry Interval in force.</param>
    /// <param name="scheme">The connection's scheme.</param>
    /// <param name="resumed">Set when a kept session was resumed: <c>CONNACK</c>'s Session Present.</param>
    /// <returns>The session, or <see langword="null"/> when a new one would pass <see cref="MaxKeptSessions"/>.</returns>
    internal Mqtt5Session? OpenSession(string clientIdentifier, string? account, bool cleanStart, TimeSpan expiry, string scheme, out bool resumed)
    {
        Mqtt5Session? ended;
        Mqtt5Session? opened = null;
        lock (sessionsGate)
        {
            ended = TakeKeptSession(clientIdentifier, account, cleanStart, out resumed);
            if (resumed)
            {
                ended!.Expiry = expiry;
                return ended;
            }

            if (expiry == TimeSpan.Zero || sessions.Values.Count(session => !session.IsConnected) < MaxKeptSessions)
            {
                opened = new Mqtt5Session(clientIdentifier, Bus.Connect(account), Bus.Names, scheme, MaxQueuedMessages, MaxQueuedPayloadBytes) { Expiry = expiry };
                sessions[clientIdentifier] = opened;
            }
        }

        ended?.Dispose();
        return opened;
    }

    // Under sessionsGate: the kept session for the identifier, stopped from expiring, and either
    // resumed (held again) or taken out of the sessions to be ended; null when none is kept.
    private Mqtt5Session? TakeKeptSession(string clientIdentifier, string? account, bool cleanStart, out bool resumed)
    {
        resumed = false;
        if (!sessions.TryGetValue(clientIdentifier, out var kept) || kept.IsConnected)
        {
            return null;
        }

        kept.ExpiryTimer!.Dispose();
        kept.ExpiryTimer = null;
        resumed = !cleanStart && kept.Account == account;
        kept.IsConnected = resumed;
        if (!resumed)
        {
            sessions.Remove(clientIdentifier);
        }

        return kept;
    }

    /// <summary>
    /// Lets go of <paramref name="session"/> as its connection closes: kept for its expiry when it
    /// has one and no later connection has replaced it, ended otherwise (<c>[MQTT-3.1.2-23]</c>).
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="timeProvider">The clock its expiry is measured on.</param>
    internal void CloseSession(Mqtt5Session session, TimeProvider timeProvider)
    {
        lock (sessionsGate)
        {
            session.IsConnected = false;
            if (sessions.TryGetValue(session.ClientIdentifier, out var current) && current == session)
            {
                if (session.Expiry > TimeSpan.Zero)
                {
                    session.ExpiryTimer = timeProvider.CreateTimer(_ => ExpireSession(session), null, session.Expiry, Timeout.InfiniteTimeSpan);
                    return;
                }

                sessions.Remove(session.ClientIdentifier);
            }
        }

        session.Dispose();
    }

    /// <summary>
    /// Ends a kept session whose expiry has passed (<c>[MQTT-4.1.0-2]</c>), unless a connection
    /// resumed it first.
    /// </summary>
    /// <param name="session">The session.</param>
    internal void ExpireSession(Mqtt5Session session)
    {
        lock (sessionsGate)
        {
            if (session.IsConnected || !((ICollection<KeyValuePair<string, Mqtt5Session>>)sessions).Remove(new(session.ClientIdentifier, session)))
            {
                return;
            }
        }

        session.Dispose();
    }

    /// <summary>
    /// Whether a session is kept or held for <paramref name="clientIdentifier"/>.
    /// </summary>
    /// <param name="clientIdentifier">The identifier.</param>
    /// <returns><see langword="true"/> when one is.</returns>
    internal bool HasSession(string clientIdentifier)
    {
        lock (sessionsGate)
        {
            return sessions.ContainsKey(clientIdentifier);
        }
    }
    /// <summary>
    /// A fresh <c>surl-</c> identifier: 32 lower-case hex digits of 16 random bytes.
    /// </summary>
    /// <returns>The identifier.</returns>
    /// <param name="identifiers">The exchange's identifier source (ADR-0124 decision 6), seedable.</param>
    internal static string NewAssignedClientIdentifier(IIdentifierRandom identifiers)
    {
        Span<byte> drawn = stackalloc byte[AssignedClientIdentifierRandomBytes];
        identifiers.NextBytes(drawn);
        return AssignedClientIdentifierPrefix + Convert.ToHexStringLower(drawn);
    }

    /// <summary>
    /// Registers a connection for the identifier its client sent. A connection already registered
    /// for it is taken over (<c>[MQTT-3.1.4-3]</c>): told through its registration's
    /// <see cref="Mqtt5ClientRegistration.TakenOverToken"/>, and the new registration's
    /// <see cref="Mqtt5ClientRegistration.PreviousEnded"/> completes once it has ended.
    /// </summary>
    /// <param name="clientIdentifier">The client's identifier.</param>
    /// <returns>The registration, which <see cref="UnregisterClient"/> takes back.</returns>
    internal Mqtt5ClientRegistration RegisterClient(string clientIdentifier)
    {
        Mqtt5ClientRegistration registration;
        Mqtt5ClientRegistration? previous;
        lock (connectedClientsGate)
        {
            connectedClients.TryGetValue(clientIdentifier, out previous);
            registration = new Mqtt5ClientRegistration(previous?.Ended ?? Task.CompletedTask);
            connectedClients[clientIdentifier] = registration;
        }

        previous?.TakeOver();
        return registration;
    }

    /// <summary>
    /// Assigns an identifier no connected client has and registers a connection for it.
    /// </summary>
    /// <param name="identifiers">The exchange's identifier source each candidate is drawn from.</param>
    /// <param name="registration">The registration, which <see cref="UnregisterClient"/> takes back.</param>
    /// <returns>The assigned identifier.</returns>
    internal string RegisterAssignedClient(IIdentifierRandom identifiers, out Mqtt5ClientRegistration registration)
    {
        registration = new Mqtt5ClientRegistration(Task.CompletedTask);
        string clientIdentifier;
        do
        {
            clientIdentifier = NewAssignedClientIdentifier(identifiers);
        }
        while (!TryAddClient(clientIdentifier, registration));

        return clientIdentifier;
    }

    private bool TryAddClient(string clientIdentifier, Mqtt5ClientRegistration registration)
    {
        lock (connectedClientsGate)
        {
            return connectedClients.TryAdd(clientIdentifier, registration);
        }
    }

    /// <summary>
    /// Whether a connection is registered for <paramref name="clientIdentifier"/>.
    /// </summary>
    /// <param name="clientIdentifier">The identifier.</param>
    /// <returns><see langword="true"/> when one is.</returns>
    internal bool IsConnected(string clientIdentifier)
    {
        lock (connectedClientsGate)
        {
            return connectedClients.ContainsKey(clientIdentifier);
        }
    }

    /// <summary>
    /// Ends a connection's registration, unless a later connection has registered the same
    /// identifier since, and lets a connection that took it over go on.
    /// </summary>
    /// <param name="clientIdentifier">The identifier it registered.</param>
    /// <param name="registration">What registering it returned.</param>
    internal void UnregisterClient(string clientIdentifier, Mqtt5ClientRegistration registration)
    {
        lock (connectedClientsGate)
        {
            ((ICollection<KeyValuePair<string, Mqtt5ClientRegistration>>)connectedClients).Remove(new(clientIdentifier, registration));
        }

        registration.End();
    }

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
    /// <c>CONNECT</c> for it requires: published at once when the old session ended,
    /// discarded when the new connection resumed it (<c>[MQTT-3.1.3-9]</c>).
    /// </summary>
    /// <param name="clientIdentifier">The identifier of the connecting client.</param>
    /// <param name="sessionResumed">Whether the new connection resumed the session.</param>
    internal void SettlePendingWill(string clientIdentifier, bool sessionResumed)
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
        if (!sessionResumed)
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
