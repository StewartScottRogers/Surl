using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Telnet;

/// <summary>
/// The TELNET server (RFC 854, RFC 855): negotiates options, reports what the client tells
/// it about its terminal and environment, then echoes each line back until the client sends
/// <c>quit</c>.
/// </summary>
/// <remarks>
/// <para>
/// ADR-0025 records how the TELNET server answers, and why.
/// </para>
/// <para>
/// <b>The server speaks first.</b> Upstream curl 8.21.0 sends no negotiation until the server
/// sends one, and sends its standard input as soon as it has it. So on connecting the server
/// sends <c>IAC WILL SUPPRESS-GO-AHEAD</c>, <c>IAC DO TERMINAL-TYPE</c>,
/// <c>IAC DO X-DISPLAY-LOCATION</c>, <c>IAC DO NEW-ENVIRON</c>, <c>IAC DO NAWS</c> and the
/// banner line <c>surl TELNET server: each line is echoed back; quit ends the session.</c>
/// </para>
/// <para>
/// <b>Options.</b> The server performs <c>SUPPRESS-GO-AHEAD</c> and <c>BINARY</c>, and agrees
/// when the client offers either, or <c>TERMINAL-TYPE</c>, <c>X-DISPLAY-LOCATION</c>,
/// <c>NEW-ENVIRON</c> or <c>NAWS</c>, the options upstream curl's <c>-t</c> sets. Every other
/// option is refused, <c>DO</c> with <c>WONT</c> and <c>WILL</c> with <c>DONT</c> (RFC 855),
/// and a request for the state an option is already in is not answered (RFC 1143). Once the
/// client performs <c>TERMINAL-TYPE</c>, <c>X-DISPLAY-LOCATION</c> or <c>NEW-ENVIRON</c> the
/// server sends <c>SEND</c> for it, unless the client has already sent <c>quit</c>. The
/// answer, and the first window size after the client turns <c>NAWS</c> on, is reported as
/// one <see cref="IExchangeLog.Note"/> on the exchange's log. Any other subnegotiation - one
/// not asked for, or a second answer to one <c>SEND</c> - is ignored unreported, so a client
/// cannot flood the log. Other two-byte commands (<c>NOP</c>, <c>AYT</c> and the rest) are
/// ignored.
/// </para>
/// <para>
/// <b>The session.</b> Each line of data, ended by LF (a CR before it dropped) or by CR NUL,
/// is sent back byte for byte, followed by CRLF; a bare CR inside it is sent back as it is.
/// <c>IAC IAC</c> in the client's data is the byte 255, and 255 in an echoed line is sent as
/// <c>IAC IAC</c> (RFC 854). A line reading <c>quit</c>, in any case, is answered
/// <c>bye</c>; the server then closes the connection once the client has answered every
/// <c>SEND</c> it made, echoing nothing more. A client that answers none keeps the
/// connection until the serving engine's idle timeout cancels the exchange (ADR-0006,
/// section 6).
/// </para>
/// <para>
/// <b>Limits.</b> <see cref="ExchangeLimits.MaxLineBytes"/> (ADR-0006, section 1) bounds a
/// line and a subnegotiation. A line longer than it, its line ending included, is answered
/// <c>line too long</c> and the connection closed, a departure from ADR-0006 section 5's
/// close with no bytes that ADR-0025 records; a subnegotiation longer than it closes the
/// connection with no answer (ADR-0006, section 5). A limit of 0 lets a line or subnegotiation grow without bound. A client that
/// closes the connection ends the session; one that closes it part way through a command
/// gets a note saying so.
/// </para>
/// </remarks>
public sealed class TelnetProtocolServer : IConnectionProtocolServer
{
    private const int ReadBufferBytes = 4096;

    /// <summary>
    /// The one scheme answered: <c>telnet</c>.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["telnet"]);

    /// <summary>
    /// Sends the opening negotiation and banner, then answers everything the client sends
    /// on <paramref name="connection"/> until the session is over or the client closes it.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var cancellationToken = context.CancellationToken;
        var session = new TelnetSession(context.Log, context.Limits.MaxLineBytes);
        var buffer = new byte[ReadBufferBytes];

        await WriteOutboundAsync(connection, session, cancellationToken);
        while (!session.IsOver)
        {
            var read = await connection.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                session.ReceiveClose();

                return;
            }

            session.Receive(buffer.AsSpan(0, read));
            await WriteOutboundAsync(connection, session, cancellationToken);
        }
    }

    private static async Task WriteOutboundAsync(IConnection connection, TelnetSession session, CancellationToken cancellationToken)
    {
        var outbound = session.TakeOutbound();
        if (outbound.Length > 0)
        {
            await connection.WriteAsync(outbound, cancellationToken);
        }
    }
}
