namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The Long Polling transport: the hub's records wait here until a <c>GET</c> poll takes them, and the
/// client's half arrives by HTTP Post (TransportProtocols.md "Long Polling"; ADR-0119, decision 2,
/// amendment 1). It carries Text and Binary.
/// </summary>
/// <param name="posts">The client's posted payloads.</param>
/// <param name="timeProvider">The clock the poll timeout runs on.</param>
internal sealed class SignalRLongPollingTransport(SignalRPostedPayloads posts, TimeProvider timeProvider) : ISignalRHubTransport
{
    /// <summary>
    /// How long a poll waits for a record before it is answered <c>200</c> with no content (ADR-0119, decision 2, amendment 1).
    /// </summary>
    public static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(90);

    private readonly Lock gate = new();
    private readonly List<byte[]> queued = [];
    private TaskCompletionSource<bool>? poll;
    private bool isEnded;

    /// <inheritdoc/>
    public SemaphoreSlim WriteGate { get; } = new(1, 1);

    /// <inheritdoc/>
    public bool CarriesBinary => true;

    /// <inheritdoc/>
    public void Dispose() => WriteGate.Dispose();

    /// <inheritdoc/>
    public Task<SignalRTransportRead> ReadAsync(CancellationToken token) => posts.ReadAsync(token);

    /// <inheritdoc/>
    public Task WriteRecordAsync(byte[] record, bool isBinary, CancellationToken token)
    {
        Enqueue(record);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task WriteEndAsync(SignalRSessionEnd end, byte[]? hubClose, bool isBinary, CancellationToken token)
    {
        posts.Close(end);
        lock (gate)
        {
            if (hubClose is not null)
            {
                queued.Add(hubClose);
            }

            isEnded = true;
            poll?.TrySetResult(true);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// A client's <c>DELETE</c>: the connection ends, and the poll waiting is completed with <c>204</c>.
    /// </summary>
    public void Delete()
    {
        posts.Close(SignalRSessionEnd.Silent("the client sent DELETE"));
        lock (gate)
        {
            queued.Clear();
            isEnded = true;
            poll?.TrySetResult(true);
        }
    }

    /// <summary>
    /// Answers one poll: the records waiting, else the next record to be sent, else <c>200</c> with no content
    /// once <see cref="PollTimeout"/> passes; an older poll still waiting is completed with <c>204</c>.
    /// </summary>
    /// <param name="token">Cancelled when the poll's request goes away.</param>
    /// <returns>The poll's answer.</returns>
    public async Task<SignalRPollResult> PollAsync(CancellationToken token)
    {
        TaskCompletionSource<bool> mine = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (gate)
        {
            poll?.TrySetResult(false);
            if (queued.Count > 0 || isEnded)
            {
                return Take();
            }

            poll = mine;
        }

        try
        {
            using var stopWaiting = CancellationTokenSource.CreateLinkedTokenSource(token);
            var timeout = Task.Delay(PollTimeout, timeProvider, stopWaiting.Token);
            var first = await Task.WhenAny(mine.Task, timeout);
            await stopWaiting.CancelAsync();
            if (first == timeout)
            {
                // Throws when the request went away instead of the timeout passing.
                await timeout;

                return new SignalRPollResult(200, []);
            }

            return await mine.Task ? TakeLocked() : new SignalRPollResult(204, []);
        }
        finally
        {
            lock (gate)
            {
                if (poll == mine)
                {
                    poll = null;
                }
            }
        }
    }

    private void Enqueue(byte[] record)
    {
        lock (gate)
        {
            queued.Add(record);
            poll?.TrySetResult(true);
        }
    }

    private SignalRPollResult TakeLocked()
    {
        lock (gate)
        {
            return Take();
        }
    }

    // The caller holds the gate. Records waiting go out together; with none left, the poll of an ended connection is 204.
    private SignalRPollResult Take()
    {
        poll = null;
        if (queued.Count == 0)
        {
            return new SignalRPollResult(204, []);
        }

        var body = queued.SelectMany(record => record).ToArray();
        queued.Clear();

        return new SignalRPollResult(200, body);
    }
}
