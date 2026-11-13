using System.Globalization;
using System.Text;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// The HTTP proxy server half of an upstream curl test case (ADR-0080 decision 2): it reads each
/// request with Surl's own HTTP/1.x head reader, reads its body as its framing declares, and
/// answers it with the case's reply part verbatim, as <see cref="HttpProxyUpstreamTestCaseReplies"/>
/// chooses it, keeping the connection open for the next request unless <c>swsclose</c> says
/// otherwise (FILEFORMAT.md at tag <c>curl-8_21_0</c>).
/// </summary>
/// <remarks>
/// <para>
/// A <c>CONNECT</c> whose reply part is a <c>2xx</c> response opens the tunnel the way upstream's
/// <c>sws</c> does: the proxy connects to <c>127.0.0.1</c> at the <c>CONNECT</c>'s port, whatever
/// host the request names, through the injected <see cref="IOriginConnector"/>, sends the reply
/// part, and relays bytes both ways until both sides close. What the proxy read before that
/// point is the case's <c>&lt;proxy&gt;</c>; what the origin server half reads is its
/// <c>&lt;protocol&gt;</c>. A <c>CONNECT</c> whose part is not <c>2xx</c> is answered with the part
/// and no tunnel opens. A tunnel that cannot be opened ends the connection with no bytes.
/// </para>
/// <para>
/// Any other request is answered with its part, as the HTTP server half would. A request whose
/// head cannot be read ends the connection with no bytes, and so does one whose reply part the case
/// does not have; the exchange log names which. Never composed by the shipped <c>surl</c>:
/// <c>Surl.Conformance.UnitTests</c> composes it for upstream's test cases.
/// </para>
/// </remarks>
/// <param name="replies">The case's reply and record of received bytes.</param>
/// <param name="originConnector">How the tunnel's origin is reached.</param>
public sealed class HttpProxyUpstreamTestCaseServer(HttpProxyUpstreamTestCaseReplies replies, IOriginConnector originConnector) : IConnectionProtocolServer
{
    private const string TunnelHost = "127.0.0.1";

    private const int MaxChunkLineBytes = 8192;

    private readonly HttpProxyUpstreamTestCaseReplies replies = replies ?? throw new ArgumentNullException(nameof(replies));
    private readonly IOriginConnector originConnector = originConnector ?? throw new ArgumentNullException(nameof(originConnector));

    /// <summary>
    /// The one scheme answered: <c>http</c>; the HTTPS proxy is the same server on a connection the
    /// engine has secured.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["http"]);

    /// <summary>
    /// Answers every request on <paramref name="connection"/> with the case's reply, recording
    /// every byte read until a tunnel opens, until the client closes it or the case closes it.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var recording = new HttpProxyUpstreamTestCaseRecordingConnection(connection, replies);
        var reader = new HttpConnectionReader(recording, context.Limits.MaxRequestHeadBytes);
        var isFirstHead = true;
        var keepsConnectionOpen = true;
        while (keepsConnectionOpen)
        {
            var result = await reader.ReadNextRequestHeadAsync(context, isFirstHead);
            isFirstHead = false;
            if (result.Head is { } head)
            {
                keepsConnectionOpen = await AnswerAsync(head, recording, reader, context);
            }
            else
            {
                context.Log.Note($"No request head was read: {result.Outcome}; closed with no bytes.");
                keepsConnectionOpen = false;
            }
        }
    }

    // False when the connection ends after this request.
    private async Task<bool> AnswerAsync(HttpRequestHead head, HttpProxyUpstreamTestCaseRecordingConnection connection, HttpConnectionReader reader, ExchangeContext context)
    {
        await ReadBodyAsync(head, reader, context.CancellationToken);
        var reply = replies.ChooseReply(head);
        if (reply.Bytes is null)
        {
            context.Log.Note($"{head.Method} {head.RequestTarget}: the case has no <{reply.PartName}> part; closed with no bytes.");
            await connection.CompleteWritesAsync(context.CancellationToken);

            return false;
        }

        if (head.Method == "CONNECT" && IsSuccessResponse(reply.Bytes))
        {
            await TunnelAsync(head, reply.Bytes, connection, reader, context);

            return false;
        }

        await connection.WriteAsync(reply.Bytes, context.CancellationToken);
        if (reply.ClosesConnection)
        {
            await connection.CompleteWritesAsync(context.CancellationToken);
        }

        return !reply.ClosesConnection;
    }

    private async Task TunnelAsync(HttpRequestHead head, byte[] replyBytes, HttpProxyUpstreamTestCaseRecordingConnection connection, HttpConnectionReader reader, ExchangeContext context)
    {
        connection.StopRecording();
        var target = ProxyTarget.FromAuthorityForm(head.RequestTarget);
        if (target is null)
        {
            context.Log.Note($"CONNECT {head.RequestTarget}: not host:port; closed with no bytes.");
            await connection.CompleteWritesAsync(context.CancellationToken);

            return;
        }

        IConnection originConnection;
        try
        {
            originConnection = await originConnector.ConnectAsync(TunnelHost, target.Port, context.Limits.HeadTimeout, context.CancellationToken);
        }
        catch (OriginConnectionException exception)
        {
            context.Log.Note($"CONNECT {head.RequestTarget}: {TunnelHost}:{target.Port} could not be reached ({exception.Failure}); closed with no bytes.");
            await connection.CompleteWritesAsync(context.CancellationToken);

            return;
        }

        await using var link = new OriginLink(TunnelHost, target.Port, originConnection);
        context.Log.Note($"Tunnelling to {TunnelHost}:{target.Port}.");
        await connection.WriteAsync(replyBytes, context.CancellationToken);
        await ProxyTunnel.RelayAsync(connection, reader, link, context.CancellationToken);
    }

    // The status code of the part's first line is 2xx.
    private static bool IsSuccessResponse(byte[] bytes) =>
        bytes.Length >= 10 && Encoding.ASCII.GetString(bytes, 0, 9) is ['H', 'T', 'T', 'P', '/', _, '.', _, ' '] && bytes[9] == (byte)'2';

    private static async Task ReadBodyAsync(HttpRequestHead head, HttpConnectionReader reader, CancellationToken cancellationToken)
    {
        var framing = HttpRequestBodyFraming.Of(head);
        if (framing.Kind == HttpRequestBodyFramingKind.ContentLength)
        {
            await ReadExactlyAsync(reader, framing.ContentLength, cancellationToken);
        }
        else if (framing.Kind == HttpRequestBodyFramingKind.Chunked)
        {
            await ReadChunksAsync(reader, cancellationToken);
        }
    }

    // False when the client half-closed before the count was read.
    private static async Task<bool> ReadExactlyAsync(HttpConnectionReader reader, long count, CancellationToken cancellationToken)
    {
        var buffer = new byte[(int)Math.Min(count, 64 * 1024) + 1];
        var left = count;
        while (left > 0)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, (int)Math.Min(left, buffer.Length)), cancellationToken);
            if (read == 0)
            {
                return false;
            }

            left -= read;
        }

        return true;
    }

    // Each chunk: its size line, its data and CRLF; the last chunk's size is 0 and the trailer
    // ends at an empty line. A line that is too long, or a size that is not hexadecimal, ends the read.
    private static async Task ReadChunksAsync(HttpConnectionReader reader, CancellationToken cancellationToken)
    {
        var size = await ReadChunkSizeAsync(reader, cancellationToken);
        while (size > 0 && await ReadExactlyAsync(reader, size + 2, cancellationToken))
        {
            size = await ReadChunkSizeAsync(reader, cancellationToken);
        }

        if (size == 0)
        {
            await ReadTrailerAsync(reader, cancellationToken);
        }
    }

    private static async Task<long> ReadChunkSizeAsync(HttpConnectionReader reader, CancellationToken cancellationToken)
    {
        var line = await reader.ReadLineAsync(MaxChunkLineBytes, cancellationToken);
        var text = line is null ? string.Empty : Encoding.Latin1.GetString(line).Split(';')[0].Trim();

        return long.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var size) && size >= 0 ? size : -1;
    }

    private static async Task ReadTrailerAsync(HttpConnectionReader reader, CancellationToken cancellationToken)
    {
        var line = await reader.ReadLineAsync(MaxChunkLineBytes, cancellationToken);
        while (line is not null && line.AsSpan().TrimEnd((byte)'\r').Length > 0)
        {
            line = await reader.ReadLineAsync(MaxChunkLineBytes, cancellationToken);
        }
    }
}
