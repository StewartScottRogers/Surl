using System.Globalization;
using System.Text;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// The proxy's FTP control connection to an origin (ADR-0088 Amendment 4): sends one command
/// line at a time and reads its reply (RFC 959 section 4.2), single- or multi-line, each within
/// the reply timeout.
/// </summary>
internal sealed class FtpControlConnection : IAsyncDisposable
{
    private const int MaxReplyLineBytes = 8192;

    private readonly IConnection connection;
    private readonly HttpConnectionReader reader;
    private readonly TimeProvider clock;
    private readonly TimeSpan replyTimeout;
    private readonly CancellationToken cancellationToken;

    /// <summary>
    /// Wraps an open control connection.
    /// </summary>
    /// <param name="connection">The connection to the origin's FTP port.</param>
    /// <param name="clock">The clock the reply timeout runs on.</param>
    /// <param name="replyTimeout">How long each reply may take; <see cref="Timeout.InfiniteTimeSpan"/> for no limit.</param>
    /// <param name="cancellationToken">The exchange's cancellation.</param>
    public FtpControlConnection(IConnection connection, TimeProvider clock, TimeSpan replyTimeout, CancellationToken cancellationToken)
    {
        this.connection = connection;
        this.clock = clock;
        this.replyTimeout = replyTimeout;
        this.cancellationToken = cancellationToken;
        reader = new HttpConnectionReader(connection, maxRequestHeadBytes: 0);
    }

    /// <summary>
    /// Sends <paramref name="command"/>, or nothing for the greeting, and reads the reply.
    /// </summary>
    /// <param name="command">The command line without its CRLF; <see langword="null"/> to read the greeting.</param>
    /// <returns>The reply's code and its last line.</returns>
    /// <exception cref="FtpOriginException">The origin did not answer in time, closed, failed or sent no FTP reply.</exception>
    public async Task<(int Code, string Line)> CommandAsync(string? command)
    {
        var step = command?.Split(' ')[0] ?? "the greeting";
        using var timeout = new CancellationTokenSource(replyTimeout, clock);
        using var timeoutOrExchange = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            if (command is not null)
            {
                await connection.WriteAsync(Encoding.UTF8.GetBytes(command + "\r\n"), timeoutOrExchange.Token);
            }

            return await ReadReplyAsync(step, timeoutOrExchange.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FtpOriginException(true, $"did not answer {step} in time");
        }
        catch (IOException)
        {
            throw new FtpOriginException(false, $"failed during {step}");
        }
    }

    /// <summary>
    /// Sends <paramref name="command"/>, or nothing for the greeting, and reads a reply that must
    /// carry one of <paramref name="codes"/>.
    /// </summary>
    /// <param name="command">The command line without its CRLF; <see langword="null"/> to read the greeting.</param>
    /// <param name="codes">The reply codes that let the fetch go on.</param>
    /// <returns>The reply's code.</returns>
    /// <exception cref="FtpOriginException">The reply carried another code, or <see cref="CommandAsync"/> failed.</exception>
    public async Task<int> ExpectAsync(string? command, params int[] codes)
    {
        var (code, line) = await CommandAsync(command);

        return codes.Contains(code)
            ? code
            : throw new FtpOriginException(false, $"answered {command?.Split(' ')[0] ?? "the greeting"} with {line}");
    }

    /// <summary>
    /// Sends <c>QUIT</c> without waiting for its reply; a connection that already failed is left as it is.
    /// </summary>
    /// <returns>A task that completes when <c>QUIT</c> is written or could not be.</returns>
    public async Task SendQuitAsync()
    {
        try
        {
            await connection.WriteAsync("QUIT\r\n"u8.ToArray(), cancellationToken);
        }
        catch (IOException)
        {
            // The origin is gone; there is nothing left to end.
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => connection.DisposeAsync();

    /// <summary>
    /// The code of a reply's first line (<c>ddd</c>, <c>ddd text</c> or <c>ddd-text</c>), or
    /// <see langword="null"/> when the line is no FTP reply.
    /// </summary>
    /// <param name="line">The line without its CRLF.</param>
    /// <returns>The code, or <see langword="null"/>.</returns>
    public static int? ReplyCode(string line) =>
        line.Length >= 3
        && (line.Length == 3 || line[3] is ' ' or '-')
        && int.TryParse(line.AsSpan(0, 3), NumberStyles.None, CultureInfo.InvariantCulture, out var code)
        && code >= 100
            ? code
            : null;

    private async Task<(int Code, string Line)> ReadReplyAsync(string step, CancellationToken token)
    {
        var line = await ReadLineAsync(step, token);
        var code = ReplyCode(line) ?? throw new FtpOriginException(false, $"answered {step} with a line that is no FTP reply");
        var last = $"{code} ";
        while (line.Length > 3 && !line.StartsWith(last, StringComparison.Ordinal))
        {
            line = await ReadLineAsync(step, token);
        }

        return (code, line);
    }

    private async Task<string> ReadLineAsync(string step, CancellationToken token)
    {
        var line = await reader.ReadLineAsync(MaxReplyLineBytes, token)
            ?? throw new FtpOriginException(false, $"closed the control connection or sent too long a line during {step}");

        return Encoding.UTF8.GetString(line).TrimEnd('\r');
    }
}
