using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ftp;

/// <summary>
/// The FTP server's control connection (RFC 959): greets the client, logs it in with
/// <c>USER</c> and <c>PASS</c> through the authentication policy, and answers its command
/// lines one after another until it sends <c>QUIT</c> or closes the connection. ADR-0052
/// records the answers.
/// </summary>
/// <remarks>
/// <para>
/// <b>The greeting</b> is <c>220 surl FTP server ready</c>, with no version (ADR-0006, section
/// 3), sent as soon as the connection is served.
/// </para>
/// <para>
/// <b>Logins.</b> Every login goes to <see cref="IAuthenticationPolicy.CheckPasswordLoginAsync"/>
/// with the listen URL's scheme, the name and password as sent and the connection's TLS session,
/// and only the policy decides; a checked login is noted as <see cref="CheckedLogin"/> says
/// (ADR-0032; ADR-0038; ADR-0052, decision 3). Until a login is accepted only <c>USER</c>,
/// <c>PASS</c>, <c>AUTH</c>, <c>PBSZ</c>, <c>PROT</c>, <c>FEAT</c>, <c>SYST</c>, <c>HELP</c>,
/// <c>NOOP</c>, <c>OPTS</c> and <c>QUIT</c> are answered; everything else is <c>530 Please log
/// in with USER and PASS</c>.
/// </para>
/// <para>
/// <b>Commands answered.</b> <c>USER</c>, <c>PASS</c>, <c>PWD</c>/<c>XPWD</c>,
/// <c>CWD</c>/<c>XCWD</c> (through the content store, so a hidden directory and <c>/.surl</c>
/// are missing), <c>CDUP</c>/<c>XCUP</c>, <c>TYPE</c>, <c>MODE</c>, <c>STRU</c>, <c>SYST</c>,
/// <c>FEAT</c>, <c>OPTS UTF8 ON</c>, <c>NOOP</c>, <c>HELP</c>, <c>ALLO</c>, <c>ACCT</c> and
/// <c>QUIT</c>. Every other command, the data-connection, transfer and TLS commands included
/// until they are built, is <c>502 Command not implemented</c>.
/// </para>
/// <para>
/// <b>Limits.</b> A command line longer than <see cref="ExchangeLimits.MaxLineBytes"/>, its line
/// ending included, is answered <c>500 Command line too long</c> and the connection is closed
/// without reading the rest. A command line not complete within
/// <see cref="ExchangeLimits.HeadTimeout"/> is answered <c>421 Timeout waiting for a
/// command</c> and the connection is closed; the first line's clock starts when the connection
/// is served, and every later line's at its first byte. Each of those two replies is written
/// within <see cref="LimitReplyWriteDeadline"/>, then writes are completed. A connection past a
/// connection limit is answered <c>421 Too many connections</c> by
/// <see cref="WriteRefusalAsync"/> (ADR-0006, section 5; ADR-0052, decision 10). A client that
/// closes the connection part way through a line gets no reply.
/// </para>
/// </remarks>
public sealed class FtpProtocolServer : IConnectionProtocolServer, IConnectionRefusalWriter
{
    /// <summary>
    /// How long the reply to a line that is too long, or late, may take to write (ADR-0006, section 5).
    /// </summary>
    public static readonly TimeSpan LimitReplyWriteDeadline = TimeSpan.FromSeconds(1);

    private const string Greeting = "220 surl FTP server ready\r\n";
    private const string RefusalReply = "421 Too many connections\r\n";
    private const string HeadTimedOutReply = "421 Timeout waiting for a command\r\n";
    private const string LineTooLongReply = "500 Command line too long\r\n";

    private readonly ContentStore contentStore;
    private readonly IAuthenticationPolicy authenticationPolicy;

    /// <summary>
    /// Creates an FTP server over <paramref name="contentStore"/> whose logins
    /// <paramref name="authenticationPolicy"/> judges.
    /// </summary>
    /// <param name="contentStore">The content store the server's paths name.</param>
    /// <param name="authenticationPolicy">Who may log in.</param>
    public FtpProtocolServer(ContentStore contentStore, IAuthenticationPolicy authenticationPolicy)
    {
        ArgumentNullException.ThrowIfNull(contentStore);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);

        this.contentStore = contentStore;
        this.authenticationPolicy = authenticationPolicy;
    }

    /// <summary>
    /// The one scheme answered so far: <c>ftp</c>.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["ftp"]);

    /// <summary>
    /// Sends the greeting, then answers every command line on <paramref name="connection"/>
    /// until the client quits or closes it, or a line is too long or late.
    /// </summary>
    /// <param name="connection">The accepted control connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        using var reader = new FtpLineReader(connection, context.Limits.MaxLineBytes, context.Limits.HeadTimeout, context.TimeProvider);
        reader.StartHeadTimeout();

        var cancellationToken = context.CancellationToken;
        await WriteAsync(connection, Greeting, cancellationToken);

        var responder = new FtpCommandResponder(connection, context, contentStore, authenticationPolicy);
        var keepsConnectionOpen = true;

        while (keepsConnectionOpen)
        {
            var result = await reader.ReadLineAsync(cancellationToken);
            keepsConnectionOpen = result.Line is { } line
                ? await responder.AnswerAsync(line)
                : await AnswerNoLineAsync(connection, context, result.Outcome);
        }
    }

    /// <summary>
    /// Answers a connection past a connection limit: <c>421 Too many connections</c> for either
    /// <see cref="ConnectionRefusal"/>, in place of the greeting, then completes writes
    /// (ADR-0052, decision 10).
    /// </summary>
    /// <param name="connection">The connection past the limit.</param>
    /// <param name="refusal">Which limit it is past; both are answered alike.</param>
    /// <param name="cancellationToken">Cancelled when the engine gives up on the refusal.</param>
    /// <returns>A task that completes when the refusal is written and writes are completed.</returns>
    public async ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await WriteAsync(connection, RefusalReply, cancellationToken);
        await connection.CompleteWritesAsync(cancellationToken);
    }

    private static async Task<bool> AnswerNoLineAsync(IConnection connection, ExchangeContext context, FtpLineReadOutcome outcome)
    {
        switch (outcome)
        {
            case FtpLineReadOutcome.LineTooLong:
                context.Log.Note($"A command line was longer than {context.Limits.MaxLineBytes} bytes; answered 500 and closed.");
                await WriteLimitReplyAsync(connection, context, LineTooLongReply);
                break;
            case FtpLineReadOutcome.HeadTimedOut:
                context.Log.Note("A command line was not complete within the head timeout; answered 421 and closed.");
                await WriteLimitReplyAsync(connection, context, HeadTimedOutReply);
                break;
            case FtpLineReadOutcome.ConnectionClosedMidLine:
                context.Log.Note("The client closed the connection part way through a command line.");
                break;
        }

        return false;
    }

    // A limit's reply gets one second to be written, and then writes are completed; a peer
    // that does not read it in time is closed all the same, never aborted (ADR-0006, section 5).
    private static async Task WriteLimitReplyAsync(IConnection connection, ExchangeContext context, string reply)
    {
        using var deadline = new CancellationTokenSource(LimitReplyWriteDeadline, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, deadline.Token);
        try
        {
            await WriteAsync(connection, reply, cancellation.Token);
            await connection.CompleteWritesAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note("The reply was not written within the one-second write deadline; the connection was closed.");
        }
    }

    private static ValueTask WriteAsync(IConnection connection, string text, CancellationToken cancellationToken) =>
        connection.WriteAsync(Encoding.ASCII.GetBytes(text), cancellationToken);
}
