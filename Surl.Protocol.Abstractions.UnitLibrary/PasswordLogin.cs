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
public sealed record PasswordLogin(
    string Scheme,
    string? UserName,
    ReadOnlyMemory<byte>? Password,
    TlsSession? TlsSession);
