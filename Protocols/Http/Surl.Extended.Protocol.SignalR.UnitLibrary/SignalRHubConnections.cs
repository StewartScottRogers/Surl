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
/// <see cref="SignalRNegotiatedConnection.ExpiresAt"/> is forgotten. BL-449 adds the upgraded
/// connections and the upgrade's lookup.
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

                return negotiated.Count;
            }
        }
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
            if (negotiated.Count >= MaxConnections)
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
