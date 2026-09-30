using Surl.LineProtocol;
using Surl.MailStore;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smtp;

/// <summary>
/// The SMTP server (RFC 5321): greets the client, answers <c>EHLO</c>, <c>HELO</c>,
/// <c>AUTH</c>, <c>MAIL</c>, <c>RCPT</c>, <c>DATA</c>, <c>RSET</c>, <c>NOOP</c>, <c>VRFY</c>, <c>EXPN</c>,
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
/// </para>
/// <para>
/// <b>TLS.</b> On a plaintext connection of a server that can upgrade, <c>EHLO</c> advertises
/// <c>STARTTLS</c>, which is answered <c>220</c>; every byte read past its line is discarded,
/// the connection is upgraded, and the session starts over (RFC 3207): no hello, no login, no
/// transaction. A server that cannot upgrade answers <c>454</c>, and a connection already on
/// TLS (after <c>STARTTLS</c>, or implicit <c>smtps</c>) <c>503</c>. A failed handshake ends
/// the exchange with the engine's note. The server tells TLS by
/// <see cref="IConnection.TlsSession"/>, never by the scheme, so it serves <c>smtps</c> when
/// registered for it through the engine's implicit TLS.
/// </para>
/// <para>
/// <b>Logins.</b> <c>EHLO</c> advertises <c>AUTH</c> with the mail authentication policy's
/// SASL mechanisms for the connection's TLS state, and leaves the line out when there are none.
/// <c>AUTH</c> (RFC 4954), allowed after <c>EHLO</c>, once, outside a transaction, runs one
/// exchange the policy decides: each challenge is sent as <c>334</c> and base64, each response
/// decoded, <c>*</c> cancels (<c>501</c>), a response that is not base64 is <c>501</c>, and the
/// end is <c>235</c>, <c>535</c>, <c>538</c> or <c>504</c> (ADR-0049, section 7), the login note
/// written first. A session logged in may send mail, and its <c>Received</c> field says
/// <c>ESMTPA</c>; <c>STARTTLS</c> logs it out. A session not logged in asks the authentication
/// policy at its first <c>MAIL</c> about the login that carries no credentials; only
/// <see cref="PasswordLoginVerdict.AcceptedUnchecked"/> (<c>--allow-anonymous</c>) lets mail in,
/// and anything else is answered <c>530</c>.
/// </para>
/// <para>
/// <b>Delivery.</b> A recipient path maps to its owner as the store's
/// <see cref="MailboxStore.LookUpRecipient"/> says; a path with no domain is read as naming
/// surl's own. A recipient that names no account is answered as one that does, and its copy is
/// discarded. The stored message is <c>Return-Path</c> and <c>Received</c> trace fields, then the
/// body as unstuffed, streamed as it is read into a <see cref="PendingMessage"/> - the store's
/// pending file - and <c>--max-filesize</c> bounds the whole. A message the store refuses is
/// answered <c>452 4.3.1</c> when the store is full, <c>552</c> when it is too large, and
/// <c>451 4.3.0 Local error in processing</c> when its file cannot be written, the reason in a
/// note; nothing is stored and the session goes on.
/// </para>
/// <para>
/// <b>Limits.</b> A command line longer than <see cref="ExchangeLimits.MaxLineBytes"/> is
/// answered <c>500</c>, one not complete within <see cref="ExchangeLimits.HeadTimeout"/>
/// <c>421</c>, and a body past <c>--max-filesize</c> <c>552</c> with nothing stored; each then
/// closes the connection, the reply written within <see cref="LimitReplyWriteDeadline"/>. An
/// exchange the engine cancels for a limit - its idle timeout or maximum duration, as
/// <see cref="ExchangeContext.IsCancelledForALimit"/> says - is answered <c>421 4.4.2 surl
/// Timeout, closing</c> within the same deadline, on that deadline alone; one cancelled at
/// shutdown ends with no farewell (ADR-0059). A connection past a connection limit is answered
/// <c>421</c> by <see cref="WriteRefusalAsync"/>.
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
    private readonly IMailAuthenticationPolicy mailAuthenticationPolicy;
    private readonly MailboxStore mailStore;
    private readonly bool isStartTlsAvailable;

    /// <summary>
    /// Creates an SMTP server that delivers into <paramref name="mailStore"/>.
    /// </summary>
    /// <param name="authenticationPolicy">Decides whether a session may send mail without a login.</param>
    /// <param name="mailAuthenticationPolicy">
    /// Says which SASL mechanisms <c>EHLO</c> advertises in the connection's TLS state, and runs
    /// each <c>AUTH</c> exchange (ADR-0049, section 6). <c>Surl.Console</c> passes the same object
    /// as <paramref name="authenticationPolicy"/>.
    /// </param>
    /// <param name="mailStore">The mail store the SMTP, IMAP and POP3 servers share.</param>
    /// <param name="isStartTlsAvailable">
    /// Whether a plaintext connection can be upgraded with <c>STARTTLS</c>: <see langword="true"/>
    /// when a server certificate is configured (<c>--cert</c> or <c>--self-signed</c>), so
    /// <c>EHLO</c> advertises it and <c>STARTTLS</c> is answered <c>220</c> and upgraded; when
    /// <see langword="false"/>, <c>STARTTLS</c> is answered <c>454</c> (ADR-0053, decision 5).
    /// </param>
    public SmtpProtocolServer(IAuthenticationPolicy authenticationPolicy, IMailAuthenticationPolicy mailAuthenticationPolicy, MailboxStore mailStore, bool isStartTlsAvailable = false)
    {
        ArgumentNullException.ThrowIfNull(authenticationPolicy);
        ArgumentNullException.ThrowIfNull(mailAuthenticationPolicy);
        ArgumentNullException.ThrowIfNull(mailStore);

        this.authenticationPolicy = authenticationPolicy;
        this.mailAuthenticationPolicy = mailAuthenticationPolicy;
        this.mailStore = mailStore;
        this.isStartTlsAvailable = isStartTlsAvailable;
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
        await new SmtpSession(connection, context, reader, authenticationPolicy, mailAuthenticationPolicy, mailStore, isStartTlsAvailable).RunAsync();
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
