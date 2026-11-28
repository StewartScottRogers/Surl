namespace Surl.Protocol.Abstractions;

/// <summary>
/// A login as a protocol server hands it to
/// <see cref="IAuthenticationPolicy.CheckPasswordLoginAsync"/> (ADR-0032, section 6): a user name
/// and clear password; a token (<see cref="IsToken"/>); or a login missing its user name, its
/// password or both. A server also builds one that no client sent, to ask what the policy allows:
/// with no user name and no password, whether a client that has not logged in is let through; and,
/// from the OCI registry, with no user name and an empty password over plain text, whether a secret
/// may arrive that way.
/// </summary>
/// <param name="Scheme">
/// The listen URL's scheme. <c>Surl.Authentication</c>'s <c>AuthenticationPolicy</c> asks an LDAP
/// account source about it when it consults one, and names the <see cref="LoginJudgedEvent"/> and
/// the login <see cref="FaultPoint"/> it makes by it when <paramref name="Exchange"/> is
/// <see langword="null"/> (otherwise by the exchange's scheme). Some servers also name the method
/// by it in their <see cref="CheckedLogin"/> log note.
/// </param>
/// <param name="UserName">
/// The user name as text: as the client sent it, except that LDAP takes the <c>uid</c> or
/// <c>cn</c> from a bind DN that leads with one, and Redis names <c>default</c> for an <c>AUTH</c>
/// carrying a password alone. Empty for a token; <see langword="null"/> when the login has none.
/// </param>
/// <param name="Password">
/// The password's or token's bytes, or <see langword="null"/> when the server passes none. A
/// missing password is not always <see langword="null"/>: FTP's <c>PASS</c> with no argument and
/// STOMP's <c>login</c> with no <c>passcode</c> pass empty bytes. STOMP passes
/// <see langword="null"/> for a <c>passcode</c> sent with no <c>login</c>.
/// </param>
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
