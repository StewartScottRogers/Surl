using Surl.LineProtocol;
using Surl.MailStore;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smtp;

/// <summary>
/// The SMTP server half of an upstream curl test case (ADR-0080 decision 2): Surl's own SMTP
/// server, <see cref="SmtpProtocolServer"/>, reading every line with its own line reader and
/// answering with its own replies, while the case's <see cref="MailUpstreamTestCaseReplies"/>
/// supplies what FILEFORMAT.md at tag <c>curl-8_21_0</c> makes the case's rather than the
/// server's.
/// </summary>
/// <remarks>
/// <para>
/// What the case changes: <c>REPLY welcome</c> replaces the greeting, and a <c>REPLY</c>'d line -
/// a command or a SASL response - is answered with the case's text and not run; <c>EHLO</c> lists
/// the case's <c>CAPA</c> capabilities and an <c>AUTH</c> line with its mechanisms, and nothing
/// of the server's own; <c>MAIL</c> needs no login, as upstream's test server needs none; a
/// <c>DATA</c> body is recorded as it was sent, dot-stuffed with its ending <c>.</c> CRLF, in
/// <see cref="MailUpstreamTestCaseReplies.UploadedBytes"/> and not delivered; <c>VRFY</c> and
/// <c>EXPN</c> answer with the case's data for the <c>EHLO</c> name when it has any. Every
/// command line is recorded.
/// </para>
/// <para>
/// Never composed by the shipped <c>surl</c>: <c>Surl.Conformance.UnitTests</c> composes it on
/// the loopback listener for upstream's test cases.
/// </para>
/// </remarks>
public sealed class SmtpUpstreamTestCaseServer : IConnectionProtocolServer
{
    private readonly SmtpProtocolServer server;

    /// <summary>
    /// Creates the server half of one case.
    /// </summary>
    /// <param name="replies">The case's reply, server commands and record of what was received.</param>
    /// <param name="authenticationPolicy">Who may log in, and the SASL exchange an un<c>REPLY</c>'d <c>AUTH</c> runs: for upstream's cases, everyone.</param>
    public SmtpUpstreamTestCaseServer(MailUpstreamTestCaseReplies replies, AnonymousAuthenticationPolicy authenticationPolicy)
    {
        ArgumentNullException.ThrowIfNull(replies);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);

        var noMail = new MailboxStore([], allowAnonymous: true, TimeProvider.System);
        server = new SmtpProtocolServer(authenticationPolicy, authenticationPolicy, noMail, isTlsUpgradeAvailable: false, replies);
    }

    /// <summary>The one scheme answered: <c>smtp</c>.</summary>
    public IReadOnlyList<string> Schemes => server.Schemes;

    /// <summary>
    /// Greets, then answers every command line on <paramref name="connection"/> until the client
    /// quits or closes it.
    /// </summary>
    /// <param name="connection">The accepted connection, plaintext or already TLS.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public Task ServeAsync(IConnection connection, ExchangeContext context) => server.ServeAsync(connection, context);
}
