using Surl.LineProtocol;
using Surl.MailStore;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Imap;

/// <summary>
/// The IMAP4rev1 server (RFC 3501): greets the client, reads tagged commands with their
/// synchronizing literals, upgrades to TLS with <c>STARTTLS</c>, logs users in with <c>LOGIN</c>
/// and <c>AUTHENTICATE</c> through the authentication policies, and answers <c>CAPABILITY</c>, <c>NOOP</c>, <c>LOGOUT</c>, <c>ID</c>, <c>NAMESPACE</c>,
/// <c>SELECT</c>, <c>EXAMINE</c>, <c>LIST</c>, <c>LSUB</c>, <c>STATUS</c>, <c>CHECK</c>,
/// <c>CLOSE</c>, <c>UNSELECT</c>, <c>FETCH</c> and <c>SEARCH</c> from the shared mail store, and
/// changes it with <c>APPEND</c>, <c>CREATE</c>, <c>DELETE</c>, <c>RENAME</c>, <c>SUBSCRIBE</c>,
/// <c>UNSUBSCRIBE</c>, <c>STORE</c>, <c>COPY</c>, <c>MOVE</c> and <c>EXPUNGE</c>, with the
/// <c>UID</c> forms of each that has one. ADR-0055 records every response.
/// </summary>
/// <remarks>
/// <para>
/// <b>Responses.</b> Each is fixed text, never an echo of the peer's bytes but its tag, the
/// mailbox names it asked about, written back in modified UTF-7, and the section and header field
/// names of a <c>FETCH</c>, in capitals; message data goes out only inside literals and quoted
/// strings. The greeting is <c>* OK [CAPABILITY ...] surl ready</c>, <c>CAPABILITY</c> answers
/// the capabilities of the connection's state, and every command this server does not answer
/// is answered <c>BAD Command not recognized</c>.
/// </para>
/// <para>
/// <b>Logins.</b> <c>LOGIN</c> is judged by <see cref="IAuthenticationPolicy.CheckPasswordLoginAsync"/>,
/// and refused as <c>NO [PRIVACYREQUIRED]</c> without asking when the mail policy does not offer
/// the clear-password login, which the capabilities then say as <c>LOGINDISABLED</c>. A command
/// that needs a login, sent before one, asks the policy once about the login that carries no
/// credentials; only <see cref="PasswordLoginVerdict.AcceptedUnchecked"/> (<c>--allow-anonymous</c>)
/// lets it through, and anything else is answered <c>NO [AUTHENTICATIONFAILED]</c>.
/// <c>AUTHENTICATE</c> runs one SASL exchange of <see cref="IMailAuthenticationPolicy"/>, its
/// initial response taken from the command line (<c>SASL-IR</c>, RFC 4959), and answers each step
/// in ADR-0049 section 7's IMAP words.
/// </para>
/// <para>
/// <b>TLS.</b> <c>STARTTLS</c> is offered and answered only on a plaintext connection of a server
/// that can upgrade, before a login: every byte the client sent after its line is discarded unrun,
/// then the connection is upgraded (ADR-0010). <c>imaps</c> is implicit TLS, which
/// <c>Surl.Console</c> wraps round this server; the session tells the two apart by
/// <see cref="IConnection.TlsSession"/>, never by the scheme (ADR-0055, decision 11).
/// </para>
/// <para>
/// <b>Limits.</b> A command, its lines and literals together, longer than
/// <see cref="ExchangeLimits.MaxLineBytes"/> is answered <c>BAD Command line too long</c> (or
/// <c>* BYE</c> when its first line is the one too long, so no tag was read) and one not complete
/// within <see cref="ExchangeLimits.HeadTimeout"/> <c>* BYE</c>; each then closes the connection,
/// the response written within <see cref="LimitReplyWriteDeadline"/>. A literal that would pass
/// the bound is answered <c>BAD Literal too long</c> before its continuation, and the session goes
/// on. <c>APPEND</c>'s message is the exception: it is checked against
/// <see cref="ExchangeLimits.MaxUploadBytes"/> (<c>NO [TOOBIG]</c> before the continuation), then
/// streamed into the store under the idle timeout alone. An exchange the engine cancels for a
/// limit - its idle timeout or maximum duration, as <see cref="ExchangeContext.IsCancelledForALimit"/>
/// says - is answered <c>* BYE surl Timeout, closing</c> within the same deadline, on that deadline
/// alone; one cancelled at shutdown ends with no farewell (ADR-0059). A connection past a
/// connection limit is answered <c>* BYE</c> by <see cref="WriteRefusalAsync"/>.
/// </para>
/// </remarks>
public sealed class ImapProtocolServer : IConnectionProtocolServer, IConnectionRefusalWriter
{
    /// <summary>
    /// How long the response to a command that is too long or late may take to write (ADR-0006,
    /// section 5).
    /// </summary>
    public static readonly TimeSpan LimitReplyWriteDeadline = TimeSpan.FromSeconds(1);

    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly IMailAuthenticationPolicy mailAuthenticationPolicy;
    private readonly MailboxStore mailStore;
    private readonly bool isTlsUpgradeAvailable;

    /// <summary>
    /// Creates an IMAP server that serves <paramref name="mailStore"/>.
    /// </summary>
    /// <param name="authenticationPolicy">Judges <c>LOGIN</c>, and whether a session may act without a login.</param>
    /// <param name="mailAuthenticationPolicy">
    /// Says which SASL mechanisms are offered on a connection in its TLS state and whether the
    /// clear-password login is, and runs each <c>AUTHENTICATE</c> exchange (ADR-0049, section 6).
    /// <c>Surl.Console</c> passes the same object as <paramref name="authenticationPolicy"/>.
    /// </param>
    /// <param name="mailStore">The mail store the SMTP, IMAP and POP3 servers share.</param>
    /// <param name="isTlsUpgradeAvailable">
    /// Whether a plaintext connection can be upgraded with <c>STARTTLS</c>: <see langword="true"/>
    /// when a server certificate is configured (<c>--cert</c> or <c>--self-signed</c>), so the
    /// capabilities advertise it and <c>STARTTLS</c> is answered <c>OK</c> and upgraded; when
    /// <see langword="false"/>, <c>STARTTLS</c> is answered <c>BAD STARTTLS not available</c>
    /// (ADR-0055, decision 11).
    /// </param>
    public ImapProtocolServer(IAuthenticationPolicy authenticationPolicy, IMailAuthenticationPolicy mailAuthenticationPolicy, MailboxStore mailStore, bool isTlsUpgradeAvailable = false)
    {
        ArgumentNullException.ThrowIfNull(authenticationPolicy);
        ArgumentNullException.ThrowIfNull(mailAuthenticationPolicy);
        ArgumentNullException.ThrowIfNull(mailStore);

        this.authenticationPolicy = authenticationPolicy;
        this.mailAuthenticationPolicy = mailAuthenticationPolicy;
        this.mailStore = mailStore;
        this.isTlsUpgradeAvailable = isTlsUpgradeAvailable;
    }

    /// <summary>
    /// The one scheme answered: <c>imap</c>.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["imap"]);

    /// <summary>
    /// Sends the greeting, then answers every command on <paramref name="connection"/> until the
    /// client logs out or closes it, or a limit is passed.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        using var reader = new CrlfLineReader(connection, context.Limits, context.TimeProvider);
        await new ImapSession(connection, context, reader, authenticationPolicy, mailAuthenticationPolicy, mailStore, isTlsUpgradeAvailable).RunAsync();
    }

    /// <summary>
    /// Answers a connection past a connection limit: <c>* BYE surl Too many connections,
    /// closing</c> in place of the greeting, for either <see cref="ConnectionRefusal"/>, then
    /// completes writes (ADR-0055, decision 12).
    /// </summary>
    /// <param name="connection">The connection past the limit.</param>
    /// <param name="refusal">Which limit it is past; both are answered alike.</param>
    /// <param name="cancellationToken">Cancelled when the engine gives up on the refusal.</param>
    /// <returns>A task that completes when the refusal is written and writes are completed.</returns>
    public async ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await ReplyLineWriter.WriteAsync(connection, ImapResponses.TooManyConnections, cancellationToken);
        await connection.CompleteWritesAsync(cancellationToken);
    }
}
