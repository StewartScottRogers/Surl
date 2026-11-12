namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// <c>open</c> and both idle time-outs (Part 2 sections 2.4.1, 2.4.5 and 2.7.1; ADR-0107 decision 3).
/// </summary>
public sealed partial class Amqp1ContainerConnection
{
    /// <summary>The least time between two empty frames, whatever the client's <c>idle-time-out</c>.</summary>
    public static readonly TimeSpan MinimumEmptyFrameSpacing = TimeSpan.FromMilliseconds(100);

    // Reads the client's open and answers it; false when the connection ended or was closed instead.
    private async ValueTask<bool> OpenAsync()
    {
        if (await ReadFrameBeforeOpenAsync() is not { } frame)
        {
            return false;
        }

        var (body, refusal) = ReadPerformative(frame);
        var performative = body?.Performative;
        refusal ??= RefuseOpen(performative);

        // Part 2 section 2.4.1: a peer that closes before it has opened sends open, then close.
        await WriteAsync(0, ContainerOpen());
        if (refusal is not null)
        {
            return await CloseWithErrorAsync(refusal);
        }

        var open = (Amqp1Open)performative!;
        clientChannelMax = open.ChannelMax;
        clientMaxFrameSize = open.MaxFrameSize;
        StartEmptyFrames(open.IdleTimeOut);
        return true;
    }

    private static Amqp1Error? RefuseOpen(Amqp1Performative? performative) => performative switch
    {
        Amqp1Open { MaxFrameSize: < Amqp1FrameLimits.MinMaxFrameSize } => Error(Amqp1ErrorCondition.InvalidField, "max-frame-size below 512"),
        Amqp1Open => null,
        _ => Error(Amqp1ErrorCondition.FramingError, "a frame other than open before open"),
    };

    private Amqp1Open ContainerOpen() => new(options.ContainerId!)
    {
        MaxFrameSize = offeredMaxFrameSize,
        ChannelMax = ContainerChannelMax,
        IdleTimeOut = options.AdvertisedIdleTimeOutMilliseconds,
        Properties = new Amqp1Map([new KeyValuePair<object?, object?>(ProductKey, ProductName)]),
    };

    // The first frame with a body; null, with nothing written, when the client went, sent a frame
    // the reader refuses (ADR-0107 decision 7: before open, a frame above 512 bytes ends it with no
    // bytes), or sent none before the head timeout ran out.
    private async ValueTask<Amqp1Frame?> ReadFrameBeforeOpenAsync()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, openDeadline);
        try
        {
            return await ReadFrameWithABodyAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
        {
            context.Log.Note("No open arrived within the head timeout; closed with no bytes.");
            return null;
        }
    }

    private async ValueTask<Amqp1Frame?> ReadFrameWithABodyAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var read = await reader.ReadFrameAsync(Amqp1FrameLimits.MinMaxFrameSize, cancellationToken);
            if (read.Frame is not { } frame)
            {
                context.Log.Note($"No open arrived ({read.Outcome}); closed with no bytes.");
                return null;
            }

            if (!frame.IsEmpty)
            {
                return frame;
            }
        }
    }

    // The next frame once open; null when the connection is over, closed here for idleness or a bad frame.
    private async ValueTask<Amqp1Frame?> ReadOpenedFrameAsync()
    {
        if (await ReadWithinIdleTimeoutAsync() is not { } read)
        {
            await CloseUnderGateAsync(Error(Amqp1ErrorCondition.ResourceLimitExceeded, "idle time-out expired"));
            return null;
        }

        if (read.RefusalCondition is { } condition)
        {
            await CloseUnderGateAsync(Error(condition, $"a malformed frame ({read.Outcome})"));
            return null;
        }

        if (read.Frame is null)
        {
            context.Log.Note($"The client went without close ({read.Outcome}).");
        }

        return read.Frame;
    }

    // Null when nothing arrived for the container's idle timeout (Part 2 section 2.4.5).
    private async ValueTask<Amqp1FrameReadResult?> ReadWithinIdleTimeoutAsync()
    {
        using var silence = new CancellationTokenSource(options.SilenceLimit, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, silence.Token);
        try
        {
            return await reader.ReadFrameAsync(offeredMaxFrameSize, cancellation.Token);
        }
        catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    // With a nonzero idle-time-out from the client, an empty frame whenever the container has written
    // nothing for half of it, at most once every MinimumEmptyFrameSpacing.
    private void StartEmptyFrames(uint? clientIdleTimeOutMilliseconds)
    {
        if (clientIdleTimeOutMilliseconds is not (> 0 and var milliseconds))
        {
            return;
        }

        var interval = TimeSpan.FromMilliseconds(Math.Max(milliseconds / 2.0, MinimumEmptyFrameSpacing.TotalMilliseconds));
        emptyFramesRunning = SendEmptyFramesAsync(interval, emptyFramesCancellation.Token);
    }

    private async Task SendEmptyFramesAsync(TimeSpan interval, CancellationToken cancellationToken)
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
                    await WriteFrameAsync(Amqp1Frame.Empty, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
