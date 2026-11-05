using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace Surl.McpServing;

/// <summary>
/// Serves one MCP session over a pair of byte streams by the stdio transport's framing (ADR-0103 decision 1):
/// newline-delimited JSON-RPC messages in, each response out as one line ending in <c>\n</c>. Up to
/// <see cref="MaximumConcurrentRequests" /> requests run at once, <c>notifications/cancelled</c> cancels the
/// named request, and the session ends when its input ends. Never touches <see cref="Console" />: the
/// <c>Surl.Mcp</c> host and the extended MCP server's <c>mcp+stdio</c> bind it to real standard streams.
/// </summary>
public sealed class McpStreamSession
{
    /// <summary>The most requests answered at once; the next message waits to be read until one finishes.</summary>
    public const int MaximumConcurrentRequests = 16;

    private const string CancelledMethod = "notifications/cancelled";

    private readonly McpInputLineReader _reader;

    private readonly Stream _output;

    private readonly McpRequestDispatcher _dispatcher;

    private readonly CancellationTokenSource _sessionCancellation;

    private readonly SemaphoreSlim _requestSlots = new(MaximumConcurrentRequests, MaximumConcurrentRequests);

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private readonly Dictionary<string, CancellationTokenSource> _runningRequests = [];

    private ExceptionDispatchInfo? _runningAnswerFailure;

    private bool _readerHoldsSlot;

    private McpStreamSession(Stream input, Stream output, McpRequestDispatcher dispatcher, int maximumMessageBytes, CancellationToken cancellationToken)
    {
        _reader = new McpInputLineReader(input, maximumMessageBytes);
        _output = output;
        _dispatcher = dispatcher;
        _sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }

    /// <summary>
    /// Serves one session until <paramref name="input" /> ends or <paramref name="cancellationToken" /> is
    /// cancelled; either way the handlers still running are cancelled and awaited first, and no response to
    /// them is written.
    /// </summary>
    /// <param name="input">The stream the client writes its messages to: standard input for stdio.</param>
    /// <param name="output">The stream the session writes only MCP messages to: standard output for stdio.</param>
    /// <param name="registry">What the server serves; every tool is added before the session starts.</param>
    /// <param name="options">Who the server is, the message limit and the list page size.</param>
    /// <param name="cancellationToken">Ends the session.</param>
    /// <returns>A task that completes when the session has ended.</returns>
    public static async Task RunAsync(
        Stream input, Stream output, McpServerRegistry registry, McpSessionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        McpRequestDispatcher dispatcher = new(registry, options);
        McpStreamSession session = new(input, output, dispatcher, options.MaximumMessageBytes, cancellationToken);
        await session.ServeAsync().ConfigureAwait(false);
    }

    // Once the caller has ended the session, how the read ended no longer matters: the session ends as when
    // the input ends. Otherwise a failed read is thrown, after the running answers have ended.
    private async Task ServeAsync()
    {
        Task reading = ReadUntilInputEndsAsync(_sessionCancellation.Token);
        await reading.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        bool callerEnded = _sessionCancellation.IsCancellationRequested;
        await EndRunningAnswersAsync().ConfigureAwait(false);
        if (!callerEnded)
        {
            await reading.ConfigureAwait(false);
        }
    }

    // Every running answer holds a slot, so holding all of them means every handler has returned.
    private async Task EndRunningAnswersAsync()
    {
        await _sessionCancellation.CancelAsync().ConfigureAwait(false);
        if (_readerHoldsSlot)
        {
            _requestSlots.Release();
        }

        for (int slot = 0; slot < MaximumConcurrentRequests; slot++)
        {
            await _requestSlots.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        _sessionCancellation.Dispose();
        _runningAnswerFailure?.Throw();
    }

    // A slot is taken before each message is read, so a 17th request waits unread: input is back-pressured.
    private async Task ReadUntilInputEndsAsync(CancellationToken sessionToken)
    {
        while (true)
        {
            await _requestSlots.WaitAsync(sessionToken).ConfigureAwait(false);
            _readerHoldsSlot = true;
            byte[]? message = await _reader.ReadAsync(sessionToken).ConfigureAwait(false);
            if (message is null)
            {
                return;
            }

            // The slot passes to the message's answer, which releases it when it finishes.
            _readerHoldsSlot = false;
            await AnswerAsync(message, sessionToken).ConfigureAwait(false);
        }
    }

    // A message answered at once (everything but a running tool call) is written before the next is read,
    // so initialize is answered before any request that follows it.
    private async Task AnswerAsync(byte[] message, CancellationToken sessionToken)
    {
        if (message.Length == 0)
        {
            await FinishAsync(Task.FromResult<byte[]?>(_dispatcher.AnswerMessageTooLong()), null, null).ConfigureAwait(false);
            return;
        }

        JsonRpcIncoming incoming = JsonRpcMessageParser.Parse(message);
        if (incoming is JsonRpcNotification { Method: CancelledMethod } cancelled)
        {
            CancelRequest(cancelled.Params);
        }

        (string? key, CancellationTokenSource? requestCancellation) = StartRequest(incoming, sessionToken);
        Task<byte[]?> answer = _dispatcher.AnswerAsync(message, requestCancellation?.Token ?? sessionToken);
        if (answer.IsCompleted)
        {
            await FinishAsync(answer, key, requestCancellation).ConfigureAwait(false);
            return;
        }

        _ = FinishRunningAsync(answer, key, requestCancellation);
    }

    // A running answer's failure (the output gone) is kept and thrown once the session has ended.
    private async Task FinishRunningAsync(Task<byte[]?> answer, string? key, CancellationTokenSource? requestCancellation)
    {
        try
        {
            await FinishAsync(answer, key, requestCancellation).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Kept and rethrown by RunAsync, never swallowed.
        catch (Exception failure)
#pragma warning restore CA1031
        {
            Interlocked.CompareExchange(ref _runningAnswerFailure, ExceptionDispatchInfo.Capture(failure), null);
        }
    }

    // A response to a cancelled request is never written: MCP's Cancellation page says it SHOULD NOT be sent.
    private async Task FinishAsync(Task<byte[]?> answer, string? key, CancellationTokenSource? requestCancellation)
    {
        try
        {
            byte[]? response = await answer.ConfigureAwait(false);
            if (response is not null && requestCancellation?.IsCancellationRequested != true)
            {
                await WriteLineAsync(response).ConfigureAwait(false);
            }
        }
        finally
        {
            EndRequest(key, requestCancellation);
            _requestSlots.Release();
        }
    }

    private (string? Key, CancellationTokenSource? Cancellation) StartRequest(JsonRpcIncoming incoming, CancellationToken sessionToken)
    {
        if (incoming is not JsonRpcRequest request)
        {
            return (null, null);
        }

        string key = RequestKey(request.Id.Value);
        CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(sessionToken);
        lock (_runningRequests)
        {
            // A client that reuses a running request's id can cancel only the first of the two.
            _runningRequests.TryAdd(key, cancellation);
        }

        return (key, cancellation);
    }

    private void EndRequest(string? key, CancellationTokenSource? cancellation)
    {
        if (key is null)
        {
            return;
        }

        lock (_runningRequests)
        {
            ((ICollection<KeyValuePair<string, CancellationTokenSource>>)_runningRequests).Remove(new(key, cancellation!));
            cancellation!.Dispose();
        }
    }

    // notifications/cancelled names the request by requestId; one that is unknown, finished or malformed is ignored.
    private void CancelRequest(JsonElement? parameters)
    {
        if (parameters is not { ValueKind: JsonValueKind.Object } value
            || !value.TryGetProperty("requestId", out JsonElement requestId)
            || requestId.ValueKind is not (JsonValueKind.String or JsonValueKind.Number))
        {
            return;
        }

        lock (_runningRequests)
        {
            if (_runningRequests.TryGetValue(RequestKey(requestId), out CancellationTokenSource? cancellation))
            {
                cancellation.Cancel();
            }
        }
    }

    // A string id and a number id never match each other; a number matches by its JSON text.
    private static string RequestKey(JsonElement id) =>
        id.ValueKind == JsonValueKind.String ? "s:" + id.GetString() : "n:" + id.GetRawText();

    // One whole line per write, behind one lock, so concurrent responses never interleave.
    private async Task WriteLineAsync(byte[] response)
    {
        byte[] line = new byte[response.Length + 1];
        response.CopyTo(line, 0);
        line[^1] = (byte)'\n';
        await _writeLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            await _output.WriteAsync(line, CancellationToken.None).ConfigureAwait(false);
            await _output.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
