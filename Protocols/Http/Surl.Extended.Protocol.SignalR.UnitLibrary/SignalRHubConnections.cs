using System.Buffers.Text;
using System.Security.Cryptography;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The connections of one hub: every listener's connections share it, so a negotiate on one TCP
/// connection names the connection a WebSocket on another takes up (TransportProtocols.md
/// "negotiate"; ADR-0119, decisions 2 and 7).
/// </summary>
/// <remarks>
/// It holds at most <see cref="MaxConnections"/>; a negotiated connection not taken up by its
/// <see cref="SignalRNegotiatedConnection.ExpiresAt"/> is forgotten. A connection a WebSocket has
/// taken up is held until <see cref="End"/>, and counts toward the ceiling meanwhile.
/// </remarks>
/// <param name="timeProvider">The clock expiry is judged on.</param>
public sealed class SignalRHubConnections(TimeProvider timeProvider)
{
    /// <summary>
    /// The most connections a hub holds, negotiated and upgraded together: ADR-0006's connection ceiling.
    /// </summary>
    public const int MaxConnections = 1024;

    private const int IdBytes = 16;

    private readonly Lock gate = new();
    private readonly Dictionary<string, SignalRNegotiatedConnection> negotiated = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SignalRNegotiatedConnection> upgraded = new(StringComparer.Ordinal);
    private readonly List<ISignalRBroadcastReceiver> receivers = [];

    /// <summary>
    /// How many connections the hub holds now, the expired ones forgotten first.
    /// </summary>
    public int Count
    {
        get
        {
            lock (gate)
            {
                ForgetExpired();

                return negotiated.Count + upgraded.Count;
            }
        }
    }

    /// <summary>
    /// Finds the connection a WebSocket upgrade's <c>id</c> names and hands it to the upgrade, or a
    /// new connection when it names none (TransportProtocols.md: "the client is not required to use
    /// the negotiate request").
    /// </summary>
    /// <param name="upgradeId">The query's <c>id</c>, or <see langword="null"/> when it has none.</param>
    /// <param name="accountName">The account the upgrade was judged as, or <see langword="null"/> for the anonymous caller.</param>
    /// <param name="connection">The connection taken up, or <see langword="null"/> when the outcome is a refusal.</param>
    /// <returns>Whether the connection was taken up, and if not, why.</returns>
    public SignalRUpgradeOutcome TakeUp(string? upgradeId, string? accountName, out SignalRNegotiatedConnection? connection)
    {
        connection = null;
        lock (gate)
        {
            ForgetExpired();
            if (upgradeId is null)
            {
                return TakeUpNew(accountName, out connection);
            }

            if (upgraded.ContainsKey(upgradeId))
            {
                return SignalRUpgradeOutcome.AlreadyTakenUp;
            }

            if (!negotiated.TryGetValue(upgradeId, out var found))
            {
                return SignalRUpgradeOutcome.NotFound;
            }

            if (found.AccountName != accountName)
            {
                return SignalRUpgradeOutcome.NegotiatedByAnotherCaller;
            }

            negotiated.Remove(upgradeId);
            upgraded.Add(upgradeId, found);
            connection = found;

            return SignalRUpgradeOutcome.TakenUp;
        }
    }

    /// <summary>
    /// Forgets a connection whose WebSocket has ended, so its <c>id</c> is then <c>404</c>.
    /// </summary>
    /// <param name="connection">The connection <see cref="TakeUp"/> handed out.</param>
    public void End(SignalRNegotiatedConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (gate)
        {
            upgraded.Remove(connection.UpgradeId);
        }
    }

    /// <summary>
    /// Adds a connection that has finished its handshake to those <see cref="Broadcast"/> reaches.
    /// </summary>
    /// <param name="receiver">The connection.</param>
    internal void Join(ISignalRBroadcastReceiver receiver)
    {
        lock (gate)
        {
            receivers.Add(receiver);
        }
    }

    /// <summary>
    /// Removes a connection from those <see cref="Broadcast"/> reaches; one never joined is ignored.
    /// </summary>
    /// <param name="receiver">The connection.</param>
    internal void Leave(ISignalRBroadcastReceiver receiver)
    {
        lock (gate)
        {
            receivers.Remove(receiver);
        }
    }

    /// <summary>
    /// Hands <paramref name="record"/> to every connection that has joined, in the order they joined
    /// (ADR-0119, decision 1: the caller included).
    /// </summary>
    /// <param name="record">The broadcast Invocation's record.</param>
    internal void Broadcast(byte[] record)
    {
        ISignalRBroadcastReceiver[] joined;
        lock (gate)
        {
            joined = [.. receivers];
        }

        foreach (var receiver in joined)
        {
            receiver.ReceiveBroadcast(record);
        }
    }

    private SignalRUpgradeOutcome TakeUpNew(string? accountName, out SignalRNegotiatedConnection? connection)
    {
        connection = null;
        if (negotiated.Count + upgraded.Count >= MaxConnections)
        {
            return SignalRUpgradeOutcome.HubFull;
        }

        connection = new SignalRNegotiatedConnection(NewId(), NewId(), accountName, DateTimeOffset.MaxValue);
        upgraded.Add(connection.UpgradeId, connection);

        return SignalRUpgradeOutcome.TakenUp;
    }

    /// <summary>
    /// Promises a new connection to the caller a negotiate request came from.
    /// </summary>
    /// <param name="accountName">The account that negotiated, or <see langword="null"/> for the anonymous caller.</param>
    /// <param name="sendsConnectionToken">Whether the response is version 1, which carries a <c>connectionToken</c>.</param>
    /// <param name="upgradeWindow">
    /// How long the connection waits for its upgrade: <c>--head-timeout</c>;
    /// <see cref="Timeout.InfiniteTimeSpan"/> means it never expires.
    /// </param>
    /// <returns>The connection, or <see langword="null"/> when the hub is full.</returns>
    public SignalRNegotiatedConnection? Negotiate(string? accountName, bool sendsConnectionToken, TimeSpan upgradeWindow)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = upgradeWindow == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : now + upgradeWindow;
        lock (gate)
        {
            ForgetExpired();
            if (negotiated.Count + upgraded.Count >= MaxConnections)
            {
                return null;
            }

            var connection = new SignalRNegotiatedConnection(NewId(), sendsConnectionToken ? NewId() : null, accountName, expiresAt);
            negotiated.Add(connection.UpgradeId, connection);

            return connection;
        }
    }

    private static string NewId() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(IdBytes));

    private void ForgetExpired()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var (upgradeId, connection) in negotiated)
        {
            if (connection.ExpiresAt <= now)
            {
                negotiated.Remove(upgradeId);
            }
        }
    }
}
