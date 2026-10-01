using System.Security.Cryptography;
using Surl.LineProtocol;
using Surl.MailStore;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Pop3;

/// <summary>
/// The POP3 server (RFC 1939, RFC 2449, RFC 2595, RFC 5034): greets the client, upgrades to TLS
/// on <c>STLS</c>, logs a user in with <c>USER</c> and <c>PASS</c>, <c>APOP</c> or SASL
/// <c>AUTH</c> through the authentication policies, and answers <c>CAPA</c>, <c>STAT</c>,
/// <c>LIST</c>, <c>UIDL</c>, <c>RETR</c>, <c>TOP</c>, <c>DELE</c>, <c>RSET</c>, <c>NOOP</c> and
/// <c>QUIT</c> against the user's maildrop in the shared mail store. ADR-0056 records every reply.
/// </summary>
/// <remarks>
/// <para>
/// <b>Replies.</b> Each is fixed text, never an echo of the peer's bytes: the greeting
/// <c>+OK surl ready</c>, followed by an <c>APOP</c> timestamp
/// <c>&lt;16 hex digits.unix seconds@surl&gt;</c> when the mail authentication policy offers
/// <c>APOP</c>; <c>CAPA</c>'s <c>TOP</c>, <c>UIDL</c>, <c>RESP-CODES</c>, <c>AUTH-RESP-CODE</c>
/// and <c>PIPELINING</c>, and before a login <c>USER</c> when the policy offers a clear password
/// on the connection, <c>SASL</c> with the mechanisms it offers, and <c>STLS</c> on a plaintext
/// connection the server can upgrade.
/// </para>
/// <para>
/// <b>TLS.</b> <c>STLS</c> is answered <c>+OK Begin TLS negotiation</c>, every byte pipelined
/// after it is thrown away unrun, and the connection is upgraded; the session starts over in the
/// AUTHORIZATION state. <c>pop3s</c> is implicit TLS that the engine completes before
/// <see cref="ServeAsync"/>: the server tells the two apart by <see cref="IConnection.TlsSession"/>,
/// never by the scheme, and <c>Surl.Console</c> registers it for <c>pop3s</c> (ADR-0056,
/// decision 8).
/// </para>
/// <para>
/// <b>Logins.</b> <c>USER</c> is accepted for any name, so no reply tells whether an account
/// exists, and refused <c>-ERR [AUTH] Encryption required</c> when no clear password is offered,
/// so the password is never sent. <c>PASS</c> goes to
/// <see cref="IAuthenticationPolicy.CheckPasswordLoginAsync"/>, and an accepted login takes the
/// owner's maildrop lock (<see cref="MailboxStore.LockMaildrop"/>) before it is answered;
/// another session's lock is <c>-ERR [IN-USE]</c>. A maildrop command before any login asks the
/// policy once about the login with no credentials, and only
/// <see cref="PasswordLoginVerdict.AcceptedUnchecked"/> (<c>--allow-anonymous</c>) opens the
/// anonymous owner's maildrop; anything else is <c>-ERR [AUTH] Authentication required</c>.
/// <c>AUTH</c> frames the SASL exchange the policy runs (<c>+ </c> continuations, base64, <c>=</c>
/// for an empty initial response, <c>*</c> to cancel), and <c>APOP</c> hands the policy the
/// greeting's timestamp; each answers in ADR-0049 section 7's POP3 words and takes the lock as
/// <c>PASS</c> does.
/// </para>
/// <para>
/// <b>The maildrop.</b> The view is fixed at the login, numbered 1 upward in UID order. <c>DELE</c>
/// marks a message, <c>RSET</c> removes the marks, and only <c>QUIT</c> after a login removes the
/// marked messages from the store; a session that ends any other way removes nothing. The lock is
/// released however the session ends.
/// </para>
/// <para>
/// <b>Limits.</b> A command line longer than <see cref="ExchangeLimits.MaxLineBytes"/> is
/// answered <c>-ERR Command line too long, closing</c>, one not complete within
/// <see cref="ExchangeLimits.HeadTimeout"/> <c>-ERR Timeout waiting for a command, closing</c>;
/// each then closes the connection, the reply written within
/// <see cref="LimitReplyWriteDeadline"/>. A connection past a connection limit is answered by
/// <see cref="WriteRefusalAsync"/>.
/// </para>
/// </remarks>
public sealed class Pop3ProtocolServer : IConnectionProtocolServer, IConnectionRefusalWriter
{
    /// <summary>
    /// How long the reply to a line that is too long or late may take to write (ADR-0006,
    /// section 5).
    /// </summary>
    public static readonly TimeSpan LimitReplyWriteDeadline = TimeSpan.FromSeconds(1);

    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly IMailAuthenticationPolicy mailAuthenticationPolicy;
    private readonly MailboxStore mailStore;
    private readonly bool isTlsUpgradeAvailable;
    private readonly RandomNumberGenerator timestampRandom;

    /// <summary>
    /// Creates a POP3 server that serves the maildrops in <paramref name="mailStore"/>.
    /// </summary>
    /// <param name="authenticationPolicy">Checks each <c>PASS</c>, and decides whether a session
    /// may read mail without a login.</param>
    /// <param name="mailAuthenticationPolicy">Says what <c>CAPA</c> and the greeting offer in the
    /// connection's TLS state - <c>USER</c>, the SASL mechanisms, an <c>APOP</c> timestamp
    /// (ADR-0049, section 2) - and runs each <c>AUTH</c> exchange and <c>APOP</c> check (ADR-0049,
    /// section 6). <c>Surl.Console</c> passes the same object as
    /// <paramref name="authenticationPolicy"/>.</param>
    /// <param name="mailStore">The mail store the SMTP, IMAP and POP3 servers share.</param>
    /// <param name="isTlsUpgradeAvailable">
    /// Whether a plaintext connection can be upgraded with <c>STLS</c>: <see langword="true"/> when
    /// a server certificate is configured (<c>--cert</c> or <c>--self-signed</c>), so <c>CAPA</c>
    /// advertises it and <c>STLS</c> is answered <c>+OK</c> and upgraded; when
    /// <see langword="false"/>, <c>STLS</c> is answered <c>-ERR STLS not available</c> (ADR-0056,
    /// decision 8).
    /// </param>
    /// <param name="timestampRandom">
    /// The source of the 8 random bytes in each greeting's <c>APOP</c> timestamp (ADR-0056,
    /// decision 2); <see langword="null"/> for the system's cryptographic generator.
    /// </param>
    public Pop3ProtocolServer(
        IAuthenticationPolicy authenticationPolicy,
        IMailAuthenticationPolicy mailAuthenticationPolicy,
        MailboxStore mailStore,
        bool isTlsUpgradeAvailable = false,
        RandomNumberGenerator? timestampRandom = null)
    {
        ArgumentNullException.ThrowIfNull(authenticationPolicy);
        ArgumentNullException.ThrowIfNull(mailAuthenticationPolicy);
        ArgumentNullException.ThrowIfNull(mailStore);

        this.authenticationPolicy = authenticationPolicy;
        this.mailAuthenticationPolicy = mailAuthenticationPolicy;
        this.mailStore = mailStore;
        this.isTlsUpgradeAvailable = isTlsUpgradeAvailable;
        this.timestampRandom = timestampRandom ?? RandomNumberGenerator.Create();
    }

    /// <summary>
    /// The one scheme answered: <c>pop3</c>.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["pop3"]);

    /// <summary>
    /// Sends the greeting, then answers every command line on <paramref name="connection"/>
    /// until the client quits or closes it, or a limit is passed.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        using var reader = new CrlfLineReader(connection, context.Limits, context.TimeProvider);
        await new Pop3Session(connection, context, reader, authenticationPolicy, mailAuthenticationPolicy, mailStore, isTlsUpgradeAvailable, timestampRandom).RunAsync();
    }

    /// <summary>
    /// Answers a connection past a connection limit: <c>-ERR surl Too many connections,
    /// closing</c> in place of the greeting, for either <see cref="ConnectionRefusal"/>, then
    /// completes writes (ADR-0056, decision 9).
    /// </summary>
    /// <param name="connection">The connection past the limit.</param>
    /// <param name="refusal">Which limit it is past; both are answered alike.</param>
    /// <param name="cancellationToken">Cancelled when the engine gives up on the refusal.</param>
    /// <returns>A task that completes when the refusal is written and writes are completed.</returns>
    public async ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await ReplyLineWriter.WriteAsync(connection, Pop3Replies.TooManyConnections, cancellationToken);
        await connection.CompleteWritesAsync(cancellationToken);
    }
}
