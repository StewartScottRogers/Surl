using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ftp;

/// <summary>
/// The FTP server half of an upstream curl test case (ADR-0080 decision 2): Surl's own FTP
/// server, <see cref="FtpProtocolServer"/>, reading every command with its own line reader and
/// answering with its own replies, its own data connections and its own TLS, while the case's
/// <see cref="FtpUpstreamTestCaseReplies"/> supplies what FILEFORMAT.md at tag
/// <c>curl-8_21_0</c> makes the case's rather than the server's.
/// </summary>
/// <remarks>
/// <para>
/// What the case changes: a <c>REPLY</c>'d command is answered with the case's text and not
/// run, and <c>REPLY welcome</c> replaces the greeting; <c>CWD</c> enters any directory and
/// <c>DELE</c> deletes any file; <c>RETR</c> sends all of <c>&lt;data&gt;</c> or
/// <c>&lt;dataNUM&gt;</c> whatever <c>REST</c> asked for, since a resuming case writes the
/// remainder as its <c>&lt;data&gt;</c>;
/// <c>LIST</c> and <c>NLST</c> send <c>&lt;data&gt;</c> as the listing, <c>SIZE</c> and
/// <c>MDTM</c> answer from <c>&lt;size&gt;</c> and <c>&lt;mdtm&gt;</c>, and <c>STOR</c> and
/// <c>APPE</c> keep the upload in <see cref="FtpUpstreamTestCaseReplies.UploadedBytes"/>. Every
/// command is answered without a login first, as upstream's test server answers them; a
/// <c>PASS</c> still goes to the authentication policy, which the test project makes accept
/// everyone. Every byte read from the control connection is recorded.
/// </para>
/// <para>
/// <c>AUTH</c> is refused, as on a listener without a certificate: upstream's <c>ftp</c> test
/// server has no TLS, and its <c>ftps</c> is TLS from the first byte, which the engine completes
/// before the server half is handed the connection.
/// </para>
/// <para>
/// Never composed by the shipped <c>surl</c>: <c>Surl.Conformance.UnitTests</c> composes it on
/// the loopback listener for upstream's test cases.
/// </para>
/// </remarks>
public sealed class FtpUpstreamTestCaseServer : IConnectionProtocolServer
{
    private readonly FtpUpstreamTestCaseReplies replies;
    private readonly FtpProtocolServer server;

    /// <summary>
    /// Creates the server half of one case.
    /// </summary>
    /// <param name="replies">The case's reply, server commands and record of received bytes.</param>
    /// <param name="authenticationPolicy">Who may log in: for upstream's cases, everyone.</param>
    public FtpUpstreamTestCaseServer(FtpUpstreamTestCaseReplies replies, IAuthenticationPolicy authenticationPolicy)
    {
        ArgumentNullException.ThrowIfNull(replies);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);

        this.replies = replies;
        var noContent = new ContentStore(
            InMemoryContentFileSystem.RootPath, new InMemoryContentFileSystem(TimeProvider.System), new ContentExposureOptions());
        server = new FtpProtocolServer(noContent, authenticationPolicy, isTlsUpgradeAvailable: false, replies);
    }

    /// <summary>
    /// The schemes answered: <c>ftp</c>, and <c>ftps</c>, whose TLS handshake the engine
    /// completes before <see cref="ServeAsync"/>.
    /// </summary>
    public IReadOnlyList<string> Schemes => server.Schemes;

    /// <summary>
    /// Answers every command line on <paramref name="connection"/> until the client quits or
    /// closes it, recording every byte read.
    /// </summary>
    /// <param name="connection">The accepted control connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return server.ServeAsync(new ReceivedBytesRecordingConnection(connection, replies), context);
    }
}
