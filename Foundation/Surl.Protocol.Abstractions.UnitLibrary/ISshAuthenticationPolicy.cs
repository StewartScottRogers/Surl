namespace Surl.Protocol.Abstractions;

/// <summary>
/// Who may log in to the SSH server, and how (ADR-0051, section 7). It sits beside
/// <see cref="IAuthenticationPolicy"/>; <c>Surl.Console</c> passes the same object to the SSH
/// server as this interface. The server owns the RFC 4252 framing, the method list, the attempt
/// count, the fixed user and service, and the public-key signature check; the policy owns which
/// accounts and keys exist, every comparison, ADR-0032 section 8's delay and the login note.
/// </summary>
public interface ISshAuthenticationPolicy
{
    /// <summary>
    /// Judges an RFC 4252 <c>none</c> request: <see cref="SshLoginOutcome.AcceptedUnchecked"/>
    /// under <c>--allow-anonymous</c>, otherwise <see cref="SshLoginOutcome.Refused"/>. Never
    /// delayed, never noted.
    /// </summary>
    /// <param name="login">The request as the client sent it.</param>
    /// <returns>Whether the client is logged in without a credential.</returns>
    SshLoginVerdict CheckSshNoneLogin(SshNoneLogin login);

    /// <summary>
    /// Judges a <c>password</c> request, or the one answer to a <c>keyboard-interactive</c>
    /// prompt. SSH encrypts before it authenticates, so the password is not a plain-text secret.
    /// </summary>
    /// <param name="login">The login as the client sent it.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Whether the login is accepted, with the note to write when a credential was checked.</returns>
    ValueTask<SshLoginVerdict> CheckSshPasswordLoginAsync(SshPasswordLogin login, CancellationToken cancellationToken);

    /// <summary>
    /// Judges a <c>publickey</c> request: whether the key is authorized for the user. The server
    /// has already judged a signed request's signature and says so in <see cref="SshPublicKeyLogin.Proof"/>.
    /// </summary>
    /// <param name="login">The login as the client sent it, with the server's verdict on its signature.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Whether the key is acceptable or the login accepted, with the note to write when a credential was checked.</returns>
    ValueTask<SshLoginVerdict> CheckSshPublicKeyLoginAsync(SshPublicKeyLogin login, CancellationToken cancellationToken);
}
