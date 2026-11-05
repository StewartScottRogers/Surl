using System.Collections.Concurrent;
using System.Security.Cryptography;

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
/// <b>Publishing.</b> Every message the broker publishes is raised as
/// <see cref="ApplicationMessagePublished"/>. Only Wills are published so far; delivery to
/// subscribers and retained messages are BL-497's and BL-498's.
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
    /// Creates the state of a broker with no client connected and no Will pending.
    /// </summary>
    public Mqtt5BrokerState()
        : this(NewAssignedClientIdentifier)
    {
    }

    /// <summary>
    /// Creates the state of a broker whose assigned client identifiers come from
    /// <paramref name="assignedClientIdentifierSource"/>.
    /// </summary>
    /// <param name="assignedClientIdentifierSource">Draws one candidate identifier per call.</param>
    internal Mqtt5BrokerState(Func<string> assignedClientIdentifierSource)
    {
        this.assignedClientIdentifierSource = assignedClientIdentifierSource;
    }

    /// <summary>
    /// Raised for every Application Message the broker publishes, on the thread that publishes it.
    /// </summary>
    internal event Action<Mqtt5ApplicationMessage>? ApplicationMessagePublished;

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
    /// Publishes <paramref name="message"/> to everything listening to the broker.
    /// </summary>
    /// <param name="message">The message.</param>
    internal void Publish(Mqtt5ApplicationMessage message) => ApplicationMessagePublished?.Invoke(message);

    /// <summary>
    /// Publishes a Will after <paramref name="delay"/>, unless a <c>CONNECT</c> for
    /// <paramref name="clientIdentifier"/> settles it first. A Will already pending for the
    /// identifier belonged to a session that has ended, so it is published at once.
    /// </summary>
    /// <param name="clientIdentifier">The identifier of the client whose Will it is.</param>
    /// <param name="will">The message the Will stands for.</param>
    /// <param name="delay">How long to wait; more than zero.</param>
    /// <param name="timeProvider">The clock the delay is measured on.</param>
    internal void ScheduleWill(string clientIdentifier, Mqtt5ApplicationMessage will, TimeSpan delay, TimeProvider timeProvider)
    {
        var pending = new PendingWill(will);
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
            Publish(replaced.Will);
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
            Publish(pending.Will);
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
        Publish(pending.Will);
    }

    private sealed class PendingWill(Mqtt5ApplicationMessage will)
    {
        public Mqtt5ApplicationMessage Will { get; } = will;

        public ITimer? Timer { get; set; }
    }
}
