namespace Surl.Extended.Protocol.Amqp0;

/// <summary>Heartbeats (AMQP 0-9-1 section 4.2.7; ADR-0112 decision 3).</summary>
internal sealed partial class Amqp0BrokerConnection
{
    // With a nonzero interval h, a peer silent for 2h is closed, and the broker writes a heartbeat
    // whenever it has written nothing for h.
    private void StartHeartbeats(ushort seconds)
    {
        if (seconds == 0)
        {
            return;
        }

        var interval = TimeSpan.FromSeconds(seconds);
        silenceLimit = interval * 2;
        heartbeatsRunning = SendHeartbeatsAsync(interval, heartbeatsCancellation.Token);
    }

    private async Task SendHeartbeatsAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var lastWrittenAt = new DateTimeOffset(Interlocked.Read(ref lastWrittenUtcTicks), TimeSpan.Zero);
                var wait = lastWrittenAt + interval - context.TimeProvider.GetUtcNow();
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, context.TimeProvider, cancellationToken);
                }
                else
                {
                    await WriteFrameAsync(Amqp0Frame.Heartbeat, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
