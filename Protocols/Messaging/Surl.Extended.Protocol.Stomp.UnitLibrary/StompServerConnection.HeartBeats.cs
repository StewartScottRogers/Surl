namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// Heart-beating once CONNECTED is written (ADR-0118 decision 7; the specification's
/// "Heart-beating"), on the injected <see cref="TimeProvider"/>.
/// </summary>
internal sealed partial class StompServerConnection
{
    private static readonly byte[] HeartBeatEol = "\n"u8.ToArray();

    /// <summary>
    /// How often the server sends an EOL in which it wrote nothing else: <c>max(1000, cy)</c>
    /// milliseconds when the client's <c>cy</c> is not 0; otherwise <see langword="null"/>.
    /// </summary>
    private TimeSpan? SendEvery => clientHeartBeat.WantsEvery == 0 ? null
        : TimeSpan.FromMilliseconds(Math.Max(StompServerOptions.HeartBeatCanSendEveryMilliseconds, clientHeartBeat.WantsEvery));

    /// <summary>
    /// How long the client may be silent before the server closes: twice <c>max(cx, sy)</c>
    /// milliseconds when the client's <c>cx</c> is not 0; otherwise <see langword="null"/>.
    /// </summary>
    private TimeSpan? Tolerance => clientHeartBeat.CanSendEvery == 0 ? null
        : 2 * TimeSpan.FromMilliseconds(Math.Max(clientHeartBeat.CanSendEvery, options.HeartBeatOffer.WantsEvery));

    private async Task HeartBeatAsync(CancellationToken cancellationToken)
    {
        reader.MarkReceived();
        try
        {
            await Task.WhenAll(SendHeartBeatsAsync(cancellationToken), WatchClientAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // The session ended: the only token the heart-beats wait on.
        }
    }

    private async Task SendHeartBeatsAsync(CancellationToken cancellationToken)
    {
        if (SendEvery is not { } interval)
        {
            return;
        }

        var seen = outbox.EnqueuedCount;
        while (true)
        {
            await Task.Delay(interval, context.TimeProvider, cancellationToken);
            if (outbox.EnqueuedCount == seen)
            {
                outbox.Enqueue(HeartBeatEol);
            }

            seen = outbox.EnqueuedCount;
        }
    }

    // A peer that stopped beating cannot be relied on to read an ERROR: close with no bytes.
    private async Task WatchClientAsync(CancellationToken cancellationToken)
    {
        if (Tolerance is not { } tolerance)
        {
            return;
        }

        while (reader.LastReceivedAt + tolerance - context.TimeProvider.GetUtcNow() is var due && due > TimeSpan.Zero)
        {
            await Task.Delay(due, context.TimeProvider, cancellationToken);
        }

        context.Log.Note($"Nothing arrived from the client for {tolerance.TotalMilliseconds} ms; closed.");
        await stopReading.CancelAsync();
    }
}
