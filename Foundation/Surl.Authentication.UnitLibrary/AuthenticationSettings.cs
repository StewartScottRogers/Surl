using Surl.Kerberos;

namespace Surl.Authentication;

/// <summary>
/// The accounts and the loosening options an <see cref="AuthenticationPolicy"/> applies
/// (ADR-0032, section 1), as <c>Surl.Composition</c> composes them from the command line.
/// </summary>
/// <param name="Accounts">
/// The account book the policy judges by. <c>Surl.Composition</c> starts the servers' book with the
/// <c>--user</c> and <c>--user-file</c> accounts, then the <c>--mailbox</c> and
/// <c>--mailbox-file</c> ones (ADR-0102, decision 1.2); the HTTP proxy's with the <c>-U</c> and
/// <c>--proxy-user-file</c> accounts instead (ADR-0088, decision 5); and that of a policy composed
/// without accounts with none. Accounts may be added to, changed in and removed from the servers'
/// and the proxy's books while the instance runs (ADR-0132).
/// </param>
/// <param name="AllowAnonymous">
/// <c>--allow-anonymous</c>: accept most requests and logins without checking credentials. Some checks
/// stay, among them: an S3 or Cloud Storage signature that is sent and any Azure Storage credential
/// that is sent are checked, a Kerberos ticket must still check against
/// <see cref="KerberosAcceptor"/>'s keys, and an LDAP DIGEST-MD5 or NTLM bind still needs the
/// account's password.
/// </param>
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
    /// The LDAP directory that checks a password login the accounts do not hold (ADR-0149, decision 6);
    /// <see langword="null"/> when <c>--user-ldap</c> is not given. Password checks ask it;
    /// <see cref="IsLoginRequiredToRead"/> counts it for the schemes it serves without contacting it.
    /// </summary>
    public LdapAccountSource? LdapAccountSource { get; init; }

    /// <summary>
    /// Whether an HTTP session challenges a request without credentials even when
    /// <see cref="IsLoginRequiredToRead"/> is <see langword="false"/> and the request is not a
    /// write, as a proxy given an empty <c>--proxy-user-file</c> must (ADR-0132 amendment 4,
    /// decision 7); <see langword="false"/> by default. The request is forbidden when no accepted
    /// method can be offered. <see cref="AllowAnonymous"/> and a connection's NTLM or Negotiate
    /// login come first, so such requests still proceed. It does not change
    /// <see cref="IsLoginRequiredToRead"/>.
    /// </summary>
    public bool RequiresLoginWithoutAccounts { get; init; }

    // The scheme of the ForScheme view these settings are; null on settings that are no view.
    private string? ViewScheme { get; init; }

    /// <summary>
    /// The scheme the HTTP family and SSH password logins ask <see cref="LdapAccountSource"/> about, whose
    /// login requests need not carry their listen URL's scheme: the scheme of the <see cref="ForScheme"/>
    /// view these settings are, so <c>ws</c>, <c>rtsp</c>, <c>sftp</c> and <c>scp</c> servers are judged by
    /// their own <c>protocols=</c> entry (ADR-0149, amendments 2 and 3), or
    /// <paramref name="familyScheme"/> on settings that are no view.
    /// </summary>
    /// <param name="familyScheme">
    /// <see cref="AuthenticationPolicy.HttpLoginScheme"/> or <see cref="AuthenticationPolicy.SshLoginScheme"/>.
    /// </param>
    /// <returns>The scheme.</returns>
    internal string DirectorySchemeFor(string familyScheme) => ViewScheme ?? familyScheme;

    /// <summary>
    /// Whether a server answering <paramref name="scheme"/> refuses unauthenticated reads (ADR-0032,
    /// section 4): when <see cref="Accounts"/> holds an account or has held one, so that removing
    /// the last account opens nothing until a reset or restore returns the accounts to a state in
    /// which there never was one (ADR-0132, amendment 5), or when the
    /// <see cref="LdapAccountSource"/> serves the scheme (ADR-0149, amendments 1 and 2).
    /// <see cref="Accounts"/> are not filtered here, so ask the <see cref="ForScheme"/> view of a
    /// scheme about that scheme.
    /// </summary>
    /// <param name="scheme">The scheme the directory is asked about.</param>
    /// <returns><see langword="true"/> when a login is required to read.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="scheme"/> is <see langword="null"/>.</exception>
    public bool IsLoginRequiredToRead(string scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);

        return Accounts.HasHeldAccount || LdapAccountSource?.Serves(scheme) == true;
    }

    /// <summary>
    /// The settings a server answering <paramref name="scheme"/> is composed with (ADR-0102,
    /// decision 4.3): <see cref="Accounts"/> as <see cref="AccountBook.ForScheme"/> views them,
    /// an <see cref="LdapAccountSource"/> the HTTP family and SSH password logins ask about
    /// <paramref name="scheme"/>, and <see cref="AuthorizedKeys"/> without the keys of the accounts that view leaves out, so
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
            ViewScheme = scheme,
            AuthorizedKeys = refused.Count == 0 ? AuthorizedKeys : AuthorizedKeys.Without(refused),
        };
    }
}
