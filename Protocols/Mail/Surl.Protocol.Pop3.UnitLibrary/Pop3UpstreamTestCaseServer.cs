using Surl.LineProtocol;
using Surl.MailStore;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Pop3;

/// <summary>
/// The POP3 server half of an upstream curl test case (ADR-0080 decision 2): Surl's own POP3
/// server, <see cref="Pop3ProtocolServer"/>, reading every line with its own line reader and
/// answering with its own replies, while the case's <see cref="MailUpstreamTestCaseReplies"/>
/// supplies what FILEFORMAT.md at tag <c>curl-8_21_0</c> makes the case's rather than the
/// server's.
/// </summary>
/// <remarks>
/// <para>
/// What the case changes: <c>REPLY welcome</c> replaces the greeting, and a <c>REPLY</c>'d line -
/// a command or a SASL response - is answered with the case's text and not run. <c>CAPA</c> lists
/// the case's <c>CAPA</c> capabilities but <c>APOP</c>, and <c>SASL</c> with its <c>AUTH</c>
/// mechanisms; with neither it is not a command. <c>USER</c> and <c>PASS</c> accept anyone, and
/// <c>APOP</c> too when the case's <c>CAPA</c> offers it. The maildrop needs no login and is the
/// case's: <c>RETR</c> and <c>TOP</c> send the data the message number picks
/// (<see cref="MailUpstreamTestCaseReplies.FindData"/>) as the case writes it, then <c>.</c> CRLF,
/// and a number with no data is no such message; <c>LIST</c> and <c>UIDL</c> send
/// <c>&lt;data&gt;</c> as the listing, and <c>LIST n</c> one line with its size; <c>STAT</c> and
/// <c>RSET</c> count one message of <c>&lt;data&gt;</c>'s size, and <c>DELE</c> deletes nothing.
/// Every command line is recorded.
/// </para>
/// <para>
/// Never composed by the shipped <c>surl</c>: <c>Surl.Conformance.UnitTests</c> composes it on
/// the loopback listener for upstream's test cases.
/// </para>
/// </remarks>
public sealed class Pop3UpstreamTestCaseServer : IConnectionProtocolServer
{
    private readonly Pop3ProtocolServer server;

    /// <summary>
    /// Creates the server half of one case.
    /// </summary>
    /// <param name="replies">The case's reply, server commands and record of what was received.</param>
    /// <param name="authenticationPolicy">The SASL exchange an un<c>REPLY</c>'d <c>AUTH</c> runs: for upstream's cases, one that accepts everyone.</param>
    public Pop3UpstreamTestCaseServer(MailUpstreamTestCaseReplies replies, AnonymousAuthenticationPolicy authenticationPolicy)
    {
        ArgumentNullException.ThrowIfNull(replies);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);

        var noMail = new MailboxStore([], allowAnonymous: true, TimeProvider.System);
        server = new Pop3ProtocolServer(authenticationPolicy, authenticationPolicy, noMail) { UpstreamTestCaseReplies = replies };
    }

    /// <summary>The one scheme answered: <c>pop3</c>.</summary>
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
