using Surl.LineProtocol;
using Surl.MailStore;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Imap;

/// <summary>
/// The IMAP server half of an upstream curl test case (ADR-0080 decision 2): Surl's own IMAP
/// server, <see cref="ImapProtocolServer"/>, reading every line with its own line reader and
/// answering with its own replies, while the case's <see cref="MailUpstreamTestCaseReplies"/>
/// supplies what FILEFORMAT.md at tag <c>curl-8_21_0</c> makes the case's rather than the
/// server's.
/// </summary>
/// <remarks>
/// <para>
/// What the case changes: <c>REPLY welcome</c> replaces the greeting, which otherwise lists no
/// capabilities, and a <c>REPLY</c>'d line - a command after its tag, or a SASL response - is
/// answered with the case's text and not run. <c>CAPABILITY</c> lists <c>IMAP4rev1</c>, the case's
/// <c>CAPA</c> capabilities and an <c>AUTH=</c> word for each <c>AUTH</c> mechanism; with neither it
/// is not a command. <c>LOGIN</c> accepts anyone, and no command needs a login. Any mailbox
/// <c>SELECT</c>s, holding one message (UIDVALIDITY 1): the data its name picks
/// (<see cref="MailUpstreamTestCaseReplies.FindData"/>), which <c>FETCH</c> and <c>UID FETCH</c>
/// send as the item asked for, <c>POSTFETCH</c>'s text before the closing parenthesis.
/// <c>LIST</c>, <c>LSUB</c>, <c>EXAMINE</c> and <c>STATUS</c> send the data their first argument
/// picks as their untagged lines, and <c>SEARCH</c> and <c>UID SEARCH</c>, <c>STORE</c> and
/// <c>COPY</c> the selected mailbox's; <c>STORE</c> echoes the flags it adds. <c>APPEND</c>'s
/// message is recorded in <see cref="MailUpstreamTestCaseReplies.UploadedBytes"/>, and the
/// commands that change mailboxes complete and change nothing. Every command line is recorded,
/// a literal's marker and bytes with it, but not <c>APPEND</c>'s message.
/// </para>
/// <para>
/// Never composed by the shipped <c>surl</c>: <c>Surl.Conformance.UnitTests</c> composes it on
/// the loopback listener for upstream's test cases.
/// </para>
/// </remarks>
public sealed class ImapUpstreamTestCaseServer : IConnectionProtocolServer
{
    private readonly ImapProtocolServer server;

    /// <summary>
    /// Creates the server half of one case.
    /// </summary>
    /// <param name="replies">The case's reply, server commands and record of what was received.</param>
    /// <param name="authenticationPolicy">The SASL exchange an un<c>REPLY</c>'d <c>AUTHENTICATE</c> runs: for upstream's cases, one that accepts everyone.</param>
    public ImapUpstreamTestCaseServer(MailUpstreamTestCaseReplies replies, AnonymousAuthenticationPolicy authenticationPolicy)
    {
        ArgumentNullException.ThrowIfNull(replies);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);

        var noMail = new MailboxStore([], allowAnonymous: true, TimeProvider.System);
        server = new ImapProtocolServer(authenticationPolicy, authenticationPolicy, noMail) { UpstreamTestCaseReplies = replies };
    }

    /// <summary>The one scheme answered: <c>imap</c>.</summary>
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
