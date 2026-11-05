using Surl.Protocol.Abstractions;

namespace Surl.Quic;

/// <summary>
/// One <see cref="QuicDatagramFlowServer.ServeAsync"/> call's loop: it waits for whichever comes
/// first - the client's next datagram, the connection's next deadline, a change the application
/// made, or the application returning - and acts on it, until the connection is closed.
/// </summary>
internal sealed class QuicDatagramFlowLoop(
    QuicApplicationConnection application,
    IDatagramFlow flow,
    IQuicApplicationProtocol applicationProtocol,
    TimeProvider timeProvider)
{
    private readonly Task neverServing = new TaskCompletionSource().Task;
    private Task? serving;

    /// <summary>Runs until the connection is closed; throws when the flow fails or <paramref name="cancellationToken"/> is cancelled.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await application.ApplyAsync(connection => connection.ReceiveDatagram(flow.FirstDatagram), cancellationToken);
        var receiving = flow.ReceiveAsync(cancellationToken).AsTask();
        while (true)
        {
            var (isClosed, isHandshakeComplete, deadline, changed) = await application.ObserveAsync(cancellationToken);
            if (isClosed)
            {
                return;
            }

            if (isHandshakeComplete)
            {
                serving ??= ServeApplicationAsync();
            }

            receiving = await WaitAndActAsync(receiving, deadline, changed, cancellationToken);
        }
    }

    /// <summary>Ends the application's side of the connection and waits for the application to return; its cancellation is not an error.</summary>
    public async Task EndAsync()
    {
        await application.EndAsync();
        if (serving is null)
        {
            return;
        }

        try
        {
            await serving;
        }
        catch (OperationCanceledException)
        {
            // The connection ended under the application; that is how it is told to stop.
        }
    }

    /// <summary>Waits for the first thing that needs the loop, acts on it, and returns the receive to wait on next.</summary>
    private async Task<Task<ReadOnlyMemory<byte>>> WaitAndActAsync(
        Task<ReadOnlyMemory<byte>> receiving, DateTimeOffset? deadline, Task changed, CancellationToken cancellationToken)
    {
        using var timer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timing = Task.Delay(DelayUntil(deadline), timeProvider, timer.Token);
        var servingOrNever = serving ?? neverServing;
        var woken = await Task.WhenAny(receiving, timing, changed, servingOrNever);
        await timer.CancelAsync();
        if (woken == receiving)
        {
            var datagram = await receiving;
            await application.ApplyAsync(connection => connection.ReceiveDatagram(datagram), cancellationToken);
            return flow.ReceiveAsync(cancellationToken).AsTask();
        }

        if (woken == timing)
        {
            await application.ApplyAsync(connection => connection.HandleTimeout(), cancellationToken);
        }
        else if (woken == servingOrNever)
        {
            await application.CloseAsync(0, cancellationToken);
        }

        return receiving;
    }

    /// <summary>How long until <paramref name="deadline"/>: none past it, and forever when there is none.</summary>
    private TimeSpan DelayUntil(DateTimeOffset? deadline)
    {
        if (deadline is not { } at)
        {
            return Timeout.InfiniteTimeSpan;
        }

        return TimeSpan.FromTicks(Math.Max(0, (at - timeProvider.GetUtcNow()).Ticks));
    }

    private async Task ServeApplicationAsync()
    {
        await Task.Yield();
        await applicationProtocol.ServeAsync(application, application.Ended);
    }
}
