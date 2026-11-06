using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// The HTTP server half of an upstream curl test case (ADR-0080 decision 2): it reads each
/// request with Surl's own HTTP/1.x head reader, reads its body as its framing declares, and
/// answers it with the case's reply part verbatim, as <see cref="HttpUpstreamTestCaseReplies"/>
/// chooses it, keeping the connection open for the next request unless <c>swsclose</c> says
/// otherwise (FILEFORMAT.md at tag <c>curl-8_21_0</c>).
/// </summary>
/// <remarks>
/// <para>
/// Server commands: <c>idle</c> sends nothing after the request and waits for the client to
/// close; <c>writedelay: [msecs]</c> pauses that long, on the exchange's
/// <see cref="ExchangeContext.TimeProvider"/>, between the reply's packets, each packet being one
/// line up to and including its LF; <c>swsclose</c>, <c>auth_required</c>, <c>no-expect</c> and
/// <c>skip:</c> are described where they act (<see cref="HttpUpstreamTestCaseReplies"/>).
/// </para>
/// <para>
/// A request whose head cannot be read ends the connection with no bytes, and so does one
/// whose reply part the case does not have; the exchange log names which. Over TLS the server
/// is the same: the serving engine secures the connection before it is handed over.
/// </para>
/// <para>
/// Never composed by the shipped <c>surl</c>: <c>Surl.Conformance.UnitTests</c> composes it on
/// the loopback listener for upstream's test cases.
/// </para>
/// </remarks>
/// <param name="replies">The case's reply, server commands and record of received bytes.</param>
public sealed class HttpUpstreamTestCaseServer(HttpUpstreamTestCaseReplies replies) : IConnectionProtocolServer
{
    private readonly HttpUpstreamTestCaseReplies replies = replies ?? throw new ArgumentNullException(nameof(replies));

    /// <summary>
    /// The one scheme answered: <c>http</c>; <c>https</c> is the same server on a connection the
    /// engine has secured.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["http"]);

    /// <summary>
    /// Answers every request on <paramref name="connection"/> with the case's reply, recording
    /// every byte read, until the client closes it or the case closes it.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var recording = new ReceivedBytesRecordingConnection(connection, replies);
        var reader = new HttpConnectionReader(recording, context.Limits.MaxRequestHeadBytes);
        var isFirstHead = true;
        var keepsConnectionOpen = true;
        while (keepsConnectionOpen)
        {
            var result = await reader.ReadNextRequestHeadAsync(context, isFirstHead);
            isFirstHead = false;
            keepsConnectionOpen = result.Head is { } head
                ? await AnswerAsync(head, recording, reader, context)
                : NoteNoHead(result.Outcome, context);
        }
    }

    private static bool NoteNoHead(HttpRequestHeadReadOutcome outcome, ExchangeContext context)
    {
        context.Log.Note($"No request head was read: {outcome}; closed with no bytes.");

        return false;
    }

    // False when the connection ends after this request.
    private async Task<bool> AnswerAsync(HttpRequestHead head, IConnection connection, HttpConnectionReader reader, ExchangeContext context)
    {
        var commands = replies.Commands;
        await HttpUpstreamTestCaseRequestBody.ReadAsync(head, commands, reader, context.CancellationToken);
        if (commands.Idle)
        {
            await WaitForClientCloseAsync(reader, context.CancellationToken);

            return false;
        }

        var reply = replies.ChooseReply(head);
        if (reply.Bytes is null)
        {
            context.Log.Note($"{head.Method} {head.RequestTarget}: the case has no <{reply.PartName}> part; closed with no bytes.");
            await connection.CompleteWritesAsync(context.CancellationToken);

            return false;
        }

        await WritePacketsAsync(connection, reply.Bytes, commands.WriteDelay, context);
        if (reply.ClosesConnection)
        {
            await connection.CompleteWritesAsync(context.CancellationToken);
        }

        return !reply.ClosesConnection;
    }

    private static async Task WaitForClientCloseAsync(HttpConnectionReader reader, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        while (await reader.ReadAsync(buffer, cancellationToken) > 0)
        {
        }
    }

    private static async Task WritePacketsAsync(IConnection connection, byte[] bytes, TimeSpan writeDelay, ExchangeContext context)
    {
        if (writeDelay == TimeSpan.Zero)
        {
            await connection.WriteAsync(bytes, context.CancellationToken);

            return;
        }

        var start = 0;
        while (start < bytes.Length)
        {
            if (start > 0)
            {
                await Task.Delay(writeDelay, context.TimeProvider, context.CancellationToken);
            }

            var lineFeed = Array.IndexOf(bytes, (byte)'\n', start);
            var end = lineFeed < 0 ? bytes.Length : lineFeed + 1;
            await connection.WriteAsync(bytes.AsMemory(start, end - start), context.CancellationToken);
            start = end;
        }
    }
}
