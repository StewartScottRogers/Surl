namespace Surl.Protocol.Abstractions;

/// <summary>
/// A login by user name and clear password, as a protocol server hands it to
/// <see cref="IAuthenticationPolicy.CheckPasswordLoginAsync"/> (ADR-0032, section 6).
/// </summary>
/// <param name="Scheme">The listen URL's scheme, for the log.</param>
/// <param name="UserName">The user name as sent, or <see langword="null"/> when the protocol sent none.</param>
/// <param name="Password">The password bytes as sent, or <see langword="null"/> when none was sent.</param>
/// <param name="TlsSession">
/// The connection's <see cref="IConnection.TlsSession"/> at the moment of the login:
/// <see langword="null"/> means unencrypted, and a server that upgrades (<c>AUTH TLS</c>,
/// <c>STARTTLS</c>) passes the session it has by then.
/// </param>
/// <param name="Exchange">The exchange the login was made in, or <see langword="null"/> when unknown.</param>
public sealed record PasswordLogin(
    string Scheme,
    string? UserName,
    ReadOnlyMemory<byte>? Password,
    TlsSession? TlsSession,
    LoginExchange? Exchange = null)
{
    /// <summary>
    /// Whether <see cref="Password"/> is a token for the empty-name account (<c>--user :tok</c>), as NATS
    /// <c>auth_token</c> and Socket.IO <c>token</c> send it (ADR-0100, decision 4.1). Without it, an empty
    /// <see cref="UserName"/> never matches an account.
    /// </summary>
    public bool IsToken { get; init; }
}
