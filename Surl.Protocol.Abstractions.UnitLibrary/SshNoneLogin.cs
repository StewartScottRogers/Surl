namespace Surl.Protocol.Abstractions;

/// <summary>
/// An SSH <c>none</c> request (RFC 4252 section 5.2), as the SSH server hands it to
/// <see cref="ISshAuthenticationPolicy.CheckSshNoneLogin"/> (ADR-0051, section 7).
/// </summary>
/// <param name="UserName">The user name as sent, or <see langword="null"/> when it is not UTF-8.</param>
public sealed record SshNoneLogin(string? UserName);
