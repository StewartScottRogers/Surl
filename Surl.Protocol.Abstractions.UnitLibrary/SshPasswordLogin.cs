namespace Surl.Protocol.Abstractions;

/// <summary>
/// An SSH <c>password</c> request (RFC 4252 section 8), or the one answer to a
/// <c>keyboard-interactive</c> prompt (RFC 4256), as the SSH server hands it to
/// <see cref="ISshAuthenticationPolicy.CheckSshPasswordLoginAsync"/> (ADR-0051, section 7).
/// </summary>
/// <param name="Method">The method as on the wire: <c>password</c> or <c>keyboard-interactive</c>.</param>
/// <param name="UserName">The user name as sent, or <see langword="null"/> when it is not UTF-8.</param>
/// <param name="Password">The password bytes as sent (UTF-8, RFC 4252 section 8).</param>
public sealed record SshPasswordLogin(string Method, string? UserName, ReadOnlyMemory<byte> Password);
