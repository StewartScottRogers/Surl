using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// What the broker keeps of one connection (ADR-0105, decision 0): its TLS session and its SASL
/// state - the login in progress after a SaslHandshake, and once it ends, who logged in.
/// </summary>
/// <param name="tlsSession">The connection's TLS session; <see langword="null"/> when unencrypted.</param>
public sealed class KafkaConnectionState(TlsSession? tlsSession)
{
    /// <summary>The connection's TLS session; <see langword="null"/> when unencrypted.</summary>
    public TlsSession? TlsSession { get; } = tlsSession;

    /// <summary>Whether a SASL login on this connection was accepted.</summary>
    public bool IsAuthenticated { get; private set; }

    /// <summary>
    /// The account an accepted login checked; <see langword="null"/> before a login, and after
    /// one accepted unchecked under <c>--allow-anonymous</c>. It is the caller topic access judges.
    /// </summary>
    public string? AccountName { get; private set; }

    /// <summary>The login a successful SaslHandshake started, until it ends; otherwise null.</summary>
    internal KafkaSaslLogin? PendingLogin { get; private set; }

    /// <summary>Starts <paramref name="login"/> after a SaslHandshake chose its mechanism.</summary>
    internal void BeginLogin(KafkaSaslLogin login) => PendingLogin = login;

    /// <summary>Ends the pending login, as accepted for <paramref name="accountName"/> or refused.</summary>
    internal void EndLogin(bool isAccepted, string? accountName)
    {
        PendingLogin = null;
        IsAuthenticated = isAccepted;
        AccountName = accountName;
    }
}
