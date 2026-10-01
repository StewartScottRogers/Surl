using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ldap;

/// <summary>
/// The LDAPv3 server (RFC 4511) upstream curl's <c>ldap://</c> transfers talk to: answers simple
/// binds, searches, compares, unbind and abandon from a read-only directory, as ADR-0072
/// decisions 2, 3, 6 and 7 say.
/// </summary>
/// <remarks>
/// <para>
/// <b>Binds.</b> A simple bind is checked through
/// <see cref="IAuthenticationPolicy.CheckPasswordLoginAsync"/>, its name mapped to an account
/// (a plain name as sent; a DN's leftmost <c>uid</c> or <c>cn</c> value): accepted,
/// <c>success</c>; a wrong password, no such user or no accounts, <c>invalidCredentials</c>
/// (49) after the policy's delay; a password over an unencrypted connection without
/// <c>--allow-plaintext-auth</c>, <c>confidentialityRequired</c> (13), unchecked; an anonymous
/// bind, <c>success</c> under <c>--allow-anonymous</c> and <c>inappropriateAuthentication</c>
/// (48) otherwise; a name with an empty password, <c>unwillingToPerform</c> (53). A version 2
/// bind - <c>WinLDAP</c>'s retry of a refused one - is answered as version 3, any other version
/// <c>protocolError</c> (2). Every Sicily and SASL bind is <c>authMethodNotSupported</c> (7)
/// until BL-309. A bind resets the connection's identity.
/// </para>
/// <para>
/// <b>Operations.</b> A search other than the root DSE's needs a bound connection, or
/// <c>--allow-anonymous</c>; otherwise it is answered <c>insufficientAccessRights</c> (50),
/// <c>bind first</c>, whether or not its base exists. Compare follows the same rule. Add,
/// delete, modify and modify DN are <c>unwillingToPerform</c>; an extended operation is
/// <c>protocolError</c> with no name; abandon has no answer, operations being answered in
/// order; unbind closes the connection. A critical control is
/// <c>unavailableCriticalExtension</c> (12). An unknown operation or a message that does not
/// decode is answered with the Notice of Disconnection <c>protocolError</c> and the
/// connection is closed.
/// </para>
/// <para>
/// <b>Limits.</b> A message longer than <see cref="ExchangeLimits.MaxMessageBytes"/> is
/// refused by its BER length before its body is read, with the Notice of Disconnection
/// <c>protocolError</c>; a first message not whole within
/// <see cref="ExchangeLimits.HeadTimeout"/> closes the connection with nothing sent; an
/// exchange the engine cancels for its idle timeout or maximum duration
/// (<see cref="ExchangeContext.IsCancelledForALimit"/>) gets the Notice of Disconnection
/// <c>unavailable</c> (52) within <see cref="LimitReplyWriteDeadline"/>; one cancelled at
/// shutdown ends with no farewell (ADR-0059).
/// </para>
/// </remarks>
public sealed class LdapProtocolServer : IConnectionProtocolServer
{
    /// <summary>
    /// How long a Notice of Disconnection may take to write (ADR-0006, section 5).
    /// </summary>
    public static readonly TimeSpan LimitReplyWriteDeadline = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The deepest a search filter may nest <c>and</c>, <c>or</c> and <c>not</c>; a deeper one
    /// is a malformed message.
    /// </summary>
    internal const int MaxFilterDepth = 64;

    private readonly LdapDirectory directory;
    private readonly IAuthenticationPolicy authenticationPolicy;

    /// <summary>
    /// Creates an LDAP server over an empty directory, as in-memory mode serves (ADR-0072
    /// decision 1): every search but the root DSE's answers <c>noSuchObject</c>.
    /// </summary>
    /// <param name="authenticationPolicy">Judges every simple bind, and says whether an unbound connection may read.</param>
    public LdapProtocolServer(IAuthenticationPolicy authenticationPolicy)
        : this(new LdapDirectory([], TimeProvider.System), authenticationPolicy)
    {
    }

    /// <summary>
    /// Creates an LDAP server over <paramref name="directory"/>.
    /// </summary>
    /// <param name="directory">The entries searches and compares read.</param>
    /// <param name="authenticationPolicy">Judges every simple bind, and says whether an unbound connection may read.</param>
    internal LdapProtocolServer(LdapDirectory directory, IAuthenticationPolicy authenticationPolicy)
    {
        ArgumentNullException.ThrowIfNull(authenticationPolicy);

        this.directory = directory;
        this.authenticationPolicy = authenticationPolicy;
    }

    /// <summary>
    /// Loads the directory from <paramref name="directoryFile"/> and creates an LDAP server over
    /// it, as surl does at start with <c>--directory</c>, after the data-directory lock and before
    /// any listener binds (ADR-0072 decision 1, ADR-0031 decision 7). A missing file serves an
    /// empty directory.
    /// </summary>
    /// <param name="directoryFile">The directory's file.</param>
    /// <param name="authenticationPolicy">Judges every simple bind, and says whether an unbound connection may read.</param>
    /// <param name="timeProvider">The clock a search's <c>timeLimit</c> is measured by.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The server.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="LdapDirectoryLoadException">The file cannot be read, or cannot be the directory.</exception>
    public static async Task<LdapProtocolServer> LoadAsync(
        LdapDirectoryFile directoryFile,
        IAuthenticationPolicy authenticationPolicy,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(directoryFile);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return new LdapProtocolServer(await directoryFile.LoadAsync(timeProvider, cancellationToken: cancellationToken), authenticationPolicy);
    }

    /// <summary>
    /// The schemes answered: <c>ldap</c>. <c>ldaps</c> joins with BL-309.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["ldap"]);

    /// <summary>
    /// Answers every message on <paramref name="connection"/> until the client unbinds or closes
    /// it, a message is refused with the Notice of Disconnection, or a limit ends it.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        return new LdapSession(connection, context, directory, authenticationPolicy).RunAsync();
    }
}
