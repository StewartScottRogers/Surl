namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The client's half of the Server-Sent Events and Long Polling transports: each HTTP Post body is one
/// payload for the session, and its request is answered once the session has processed it
/// (TransportProtocols.md "HTTP Post"; ADR-0119, decision 2, amendment 1).
/// </summary>
/// <remarks>
/// One post is outstanding at a time: a second one while the first is unprocessed is a conflict.
/// The session processes a payload before it asks for the next, so asking again is the acknowledgement.
/// </remarks>
internal sealed class SignalRPostedPayloads
{
    private readonly Lock gate = new();
    private TaskCompletionSource<bool>? outstanding;
    private TaskCompletionSource<byte[]?>? waiting;
    private byte[]? pending;
    private bool isDelivered;
    private SignalRSessionEnd? closedBecause;

    /// <summary>
    /// Offers one post's body to the session.
    /// </summary>
    /// <param name="payload">The body's bytes.</param>
    /// <returns>The task is <see langword="null"/> when another post is still outstanding; otherwise it completes <see langword="true"/> once the session processed the payload and <see langword="false"/> when the connection ended first.</returns>
    public Task<bool>? Post(byte[] payload)
    {
        lock (gate)
        {
            if (closedBecause is not null)
            {
                return Task.FromResult(false);
            }

            if (outstanding is not null)
            {
                return null;
            }

            outstanding = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (waiting is { } reader)
            {
                waiting = null;
                isDelivered = true;
                reader.SetResult(payload);
            }
            else
            {
                pending = payload;
            }

            return outstanding.Task;
        }
    }

    /// <summary>
    /// Waits for the next payload, first acknowledging the last one as processed.
    /// </summary>
    /// <param name="token">Stops the wait.</param>
    /// <returns>The payload, or how the connection ended.</returns>
    public async Task<SignalRTransportRead> ReadAsync(CancellationToken token)
    {
        TaskCompletionSource<byte[]?> source;
        lock (gate)
        {
            if (isDelivered)
            {
                isDelivered = false;
                outstanding!.SetResult(true);
                outstanding = null;
            }

            if (closedBecause is { } end)
            {
                return SignalRTransportRead.Ended(end);
            }

            if (pending is { } payload)
            {
                pending = null;
                isDelivered = true;

                return SignalRTransportRead.OfPayload(payload);
            }

            source = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
            waiting = source;
        }

        using var registration = token.Register(() => source.TrySetCanceled(token));

        return await source.Task is { } bytes ? SignalRTransportRead.OfPayload(bytes) : SignalRTransportRead.Ended(closedBecause!);
    }

    /// <summary>
    /// Ends the connection: the session's next read is <paramref name="end"/>, and a post still outstanding is answered as ended.
    /// </summary>
    /// <param name="end">How the connection ended.</param>
    public void Close(SignalRSessionEnd end)
    {
        lock (gate)
        {
            closedBecause ??= end;
            outstanding?.TrySetResult(false);
            waiting?.TrySetResult(null);
        }
    }
}
