using System.Text.Json;
using Surl.McpServing;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// One Streamable HTTP session (ADR-0111 decision 4.5): its <c>Mcp-Session-Id</c>, the login that opened it,
/// when it was last used, and the core session that answers its messages. At most
/// <see cref="MaximumConcurrentRequests" /> requests run at once; a later one waits for a slot (decision 7.3).
/// <c>notifications/cancelled</c> cancels a running request it names, and <see cref="End" /> cancels them all.
/// </summary>
internal sealed class McpHttpSession
{
    /// <summary>The most requests one session runs at once.</summary>
    public const int MaximumConcurrentRequests = 16;

    private const string CancelledMethod = "notifications/cancelled";

    private readonly McpServerSession _session;

    private readonly CancellationTokenSource _ending = new();

    private readonly SemaphoreSlim _requestSlots = new(MaximumConcurrentRequests, MaximumConcurrentRequests);

    private readonly Dictionary<string, CancellationTokenSource> _runningRequests = [];

    /// <summary>Initializes a new instance of the <see cref="McpHttpSession" /> class.</summary>
    /// <param name="id">The session's <c>Mcp-Session-Id</c>.</param>
    /// <param name="login">The account that opened it; <see langword="null" /> for the anonymous caller.</param>
    /// <param name="session">The core session, already initialized.</param>
    /// <param name="lastUsed">When it was opened.</param>
    public McpHttpSession(string id, string? login, McpServerSession session, DateTimeOffset lastUsed)
    {
        Id = id;
        Login = login;
        _session = session;
        LastUsed = lastUsed;
    }

    /// <summary>Gets the session's <c>Mcp-Session-Id</c>.</summary>
    public string Id { get; }

    /// <summary>Gets the account that opened the session; <see langword="null" /> for the anonymous caller.</summary>
    public string? Login { get; }

    /// <summary>Gets or sets when the session was last used; the session table reads and sets it under its lock.</summary>
    public DateTimeOffset LastUsed { get; set; }

    /// <summary>Takes a notification or a client's response, which gets no answer.</summary>
    /// <param name="incoming">The message as parsed.</param>
    /// <param name="message">The message's bytes.</param>
    /// <param name="cancellationToken">The exchange's cancellation.</param>
    /// <returns>A task that completes once the core has taken the message.</returns>
    public Task AcceptAsync(JsonRpcIncoming incoming, ReadOnlyMemory<byte> message, CancellationToken cancellationToken)
    {
        if (incoming is JsonRpcNotification { Method: CancelledMethod } cancelled)
        {
            CancelRequest(cancelled.Params);
            return Task.CompletedTask;
        }

        return _session.AnswerAsync(message, cancellationToken);
    }

    /// <summary>Answers a request through the core.</summary>
    /// <param name="request">The request as parsed.</param>
    /// <param name="message">The message's bytes.</param>
    /// <param name="cancellationToken">The exchange's cancellation.</param>
    /// <returns>The response, or <see langword="null" /> when the request was cancelled.</returns>
    public async Task<byte[]?> AnswerRequestAsync(JsonRpcRequest request, ReadOnlyMemory<byte> message, CancellationToken cancellationToken)
    {
        await _requestSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(_ending.Token, cancellationToken);
            string key = RequestKey(request.Id.Value);
            lock (_runningRequests)
            {
                // A client that reuses a running request's id can cancel only the first of the two.
                _runningRequests.TryAdd(key, cancellation);
            }

            try
            {
                byte[]? response = await _session.AnswerAsync(message, cancellation.Token).ConfigureAwait(false);
                return cancellation.IsCancellationRequested ? null : response;
            }
            finally
            {
                lock (_runningRequests)
                {
                    ((ICollection<KeyValuePair<string, CancellationTokenSource>>)_runningRequests).Remove(new(key, cancellation));
                }
            }
        }
        finally
        {
            _requestSlots.Release();
        }
    }

    /// <summary>Ends the session: every request still running is cancelled.</summary>
    public void End() => _ending.Cancel();

    // notifications/cancelled names the request by requestId; one that is unknown, finished or malformed is ignored.
    private void CancelRequest(JsonElement? parameters)
    {
        if (RequestIdOf(parameters) is not { } requestId)
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

    private static JsonElement? RequestIdOf(JsonElement? parameters)
    {
        if (parameters is not { ValueKind: JsonValueKind.Object } value || !value.TryGetProperty("requestId", out JsonElement requestId))
        {
            return null;
        }

        return requestId.ValueKind is JsonValueKind.String or JsonValueKind.Number ? requestId : null;
    }

    // A string id and a number id never match each other; a number matches by its JSON text.
    private static string RequestKey(JsonElement id) =>
        id.ValueKind == JsonValueKind.String ? "s:" + id.GetString() : "n:" + id.GetRawText();
}
