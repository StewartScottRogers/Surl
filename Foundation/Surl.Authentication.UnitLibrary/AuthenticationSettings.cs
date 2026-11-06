using Surl.Kerberos;

namespace Surl.Authentication;

/// <summary>
/// The accounts and the loosening options an <see cref="AuthenticationPolicy"/> applies
/// (ADR-0032, section 1), as <c>Surl.Console</c> composes them from the command line.
/// </summary>
/// <param name="Accounts">Every account from <c>--user</c> and <c>--user-file</c>.</param>
/// <param name="AllowAnonymous"><c>--allow-anonymous</c>: accept every request and login unchecked.</param>
/// <param name="AllowPlaintextAuthentication"><c>--allow-plaintext-auth</c>: accept plain-text secrets without TLS.</param>
/// <param name="AcceptedMethods">
/// <c>--auth</c>'s methods, <see cref="AuthenticationMethods.DefaultAccepted"/> when it is not given.
/// </param>
public sealed record AuthenticationSettings(
    AccountBook Accounts,
    bool AllowAnonymous,
    bool AllowPlaintextAuthentication,
    IReadOnlySet<AuthenticationMethod> AcceptedMethods)
{
    /// <summary>
    /// The public keys each user may log in to the SSH server with, from every
    /// <c>--authorized-keys</c> (ADR-0051, section 6); <see cref="AuthorizedKeyBook.Empty"/> when none is given.
    /// </summary>
    public AuthorizedKeyBook AuthorizedKeys { get; init; } = AuthorizedKeyBook.Empty;

    /// <summary>
    /// What checks a client's Kerberos AP-REQ against the service keys of <c>--keytab</c>
    /// (ADR-0057, decisions 1 and 6); <see langword="null"/> when no keytab is given. SASL
    /// <c>GSSAPI</c> is offered and run only when it is set (decision 9).
    /// </summary>
    public KerberosAcceptor? KerberosAcceptor { get; init; }

    /// <summary>
    /// The settings a server answering <paramref name="scheme"/> is composed with (ADR-0102,
    /// decision 4.3): <see cref="Accounts"/> as <see cref="AccountBook.ForScheme"/> views them,
    /// and <see cref="AuthorizedKeys"/> without the keys of the accounts that view leaves out, so
    /// every login method - password, SASL, Digest, NTLM, Kerberos's account check, SSH public
    /// keys - refuses an account restricted to other schemes exactly as it refuses no such
    /// account. The view reads the same changing accounts, so an account added or removed while
    /// the server runs is judged through it at once (ADR-0132); the authorized keys are configured
    /// at start and do not change with the accounts. An <see cref="AuthenticationPolicy"/> and the HTTP methods built over the view's
    /// <see cref="Accounts"/> judge that server's logins.
    /// </summary>
    /// <param name="scheme">The scheme the server answers, compared without regard to case.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="scheme"/> is <see langword="null"/>.</exception>
    public AuthenticationSettings ForScheme(string scheme)
    {
        var refused = Accounts.NamesRefusedOn(scheme);

        return this with
        {
            Accounts = Accounts.ForScheme(scheme),
            AuthorizedKeys = refused.Count == 0 ? AuthorizedKeys : AuthorizedKeys.Without(refused),
        };
    }
}
