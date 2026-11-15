namespace Surl.Protocol.Abstractions;

/// <summary>
/// A Kerberos login a server has checked and asks the policy to judge (ADR-0165, decision 8). It
/// carries strings so this library references no Kerberos type.
/// </summary>
/// <param name="ClientPrincipal">The client principal, or <see langword="null"/> when none could be read.</param>
/// <param name="IsTokenAccepted">Whether the Kerberos acceptor accepted the token.</param>
/// <param name="Scheme">The scheme the login was made over, such as <c>socks5</c>.</param>
/// <param name="Exchange">The exchange the login was made in, or <see langword="null"/> when unknown.</param>
public sealed record KerberosLogin(
    string? ClientPrincipal, bool IsTokenAccepted, string Scheme, LoginExchange? Exchange);
