using Surl.LineProtocol;
using Surl.MailStore;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smtp;

/// <summary>
/// The SMTP server (RFC 5321): greets the client, answers <c>EHLO</c>, <c>HELO</c>,
/// <c>MAIL</c>, <c>RCPT</c>, <c>DATA</c>, <c>RSET</c>, <c>NOOP</c>, <c>VRFY</c>, <c>EXPN</c>,
/// <c>HELP</c> and <c>QUIT</c>, and delivers each accepted message into the shared mail store.
/// ADR-0053 records every reply.
/// </summary>
/// <remarks>
/// <para>
/// <b>Replies.</b> Each is fixed text, never an echo of the peer's bytes: the greeting
/// <c>220 surl ESMTP ready</c>; <c>EHLO</c>'s capabilities <c>SIZE</c> (<c>--max-filesize</c>),
/// <c>8BITMIME</c>, <c>SMTPUTF8</c>, <c>PIPELINING</c> and <c>ENHANCEDSTATUSCODES</c>; enhanced
/// status codes on every reply but the greeting and <c>EHLO</c>'s and <c>HELO</c>'s. <c>VRFY</c>
/// and <c>EXPN</c> answer <c>252</c> whatever is asked, so no peer can enumerate accounts.
/// <c>STARTTLS</c> is answered <c>454</c> (<c>503</c> on a TLS connection) and <c>AUTH</c>
/// <c>502</c>: the upgrade and the logins are BL-199's and BL-200's.
/// </para>
/// <para>
/// <b>Logins.</b> The first <c>MAIL</c> of a session asks the authentication policy about the
/// login that carries no credentials; only <see cref="PasswordLoginVerdict.AcceptedUnchecked"/>
/// (<c>--allow-anonymous</c>) lets mail in, and anything else is answered <c>530</c>.
/// </para>
/// <para>
/// <b>Delivery.</b> A recipient path maps to its owner as the store's
/// <see cref="MailboxStore.LookUpRecipient"/> says; a path with no domain is read as naming
/// surl's own. A recipient that names no account is answered as one that does, and its copy is
/// discarded. The stored message is <c>Return-Path</c> and <c>Received</c> trace fields, then the
/// body as unstuffed, and <c>--max-filesize</c> bounds the whole.
/// </para>
/// <para>
/// <b>Limits.</b> A command line longer than <see cref="ExchangeLimits.MaxLineBytes"/> is
/// answered <c>500</c>, one not complete within <see cref="ExchangeLimits.HeadTimeout"/>
/// <c>421</c>, and a body past <c>--max-filesize</c> <c>552</c> with nothing stored; each then
/// closes the connection, the reply written within <see cref="LimitReplyWriteDeadline"/>. A
/// connection past a connection limit is answered <c>421</c> by <see cref="WriteRefusalAsync"/>.
/// </para>
/// </remarks>
public sealed class SmtpProtocolServer : IConnectionProtocolServer, IConnectionRefusalWriter
{
    /// <summary>
    /// How long the reply to a line that is too long or late, or a message that is too large, may
    /// take to write (ADR-0006, section 5).
    /// </summary>
    public static readonly TimeSpan LimitReplyWriteDeadline = TimeSpan.FromSeconds(1);

    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly MailboxStore mailStore;

    /// <summary>
    /// Creates an SMTP server that delivers into <paramref name="mailStore"/>.
    /// </summary>
    /// <param name="authenticationPolicy">Decides whether a session may send mail without a login.</param>
    /// <param name="mailStore">The mail store the SMTP, IMAP and POP3 servers share.</param>
    public SmtpProtocolServer(IAuthenticationPolicy authenticationPolicy, MailboxStore mailStore)
    {
        ArgumentNullException.ThrowIfNull(authenticationPolicy);
        ArgumentNullException.ThrowIfNull(mailStore);

        this.authenticationPolicy = authenticationPolicy;
        this.mailStore = mailStore;
    }

    /// <summary>
    /// The one scheme answered: <c>smtp</c>.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["smtp"]);

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
        await new SmtpSession(connection, context, reader, authenticationPolicy, mailStore).RunAsync();
    }

    /// <summary>
    /// Answers a connection past a connection limit: <c>421 4.3.2 surl Too many connections,
    /// closing</c> in place of the greeting, for either <see cref="ConnectionRefusal"/>, then
    /// completes writes (ADR-0053, decision 7).
    /// </summary>
    /// <param name="connection">The connection past the limit.</param>
    /// <param name="refusal">Which limit it is past; both are answered alike.</param>
    /// <param name="cancellationToken">Cancelled when the engine gives up on the refusal.</param>
    /// <returns>A task that completes when the refusal is written and writes are completed.</returns>
    public async ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await ReplyLineWriter.WriteAsync(connection, SmtpReplies.TooManyConnections, cancellationToken);
        await connection.CompleteWritesAsync(cancellationToken);
    }
}
