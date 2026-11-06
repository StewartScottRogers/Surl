namespace Surl.Http2;

/// <summary>
/// The flow-control windows of what the server sends on one HTTP/2 connection (RFC 9113
/// sections 5.2 and 6.9): the connection's and each open stream's, which the client's
/// <c>WINDOW_UPDATE</c> frames and <c>SETTINGS_INITIAL_WINDOW_SIZE</c> change, and a wait for
/// room in both before <c>DATA</c> is sent.
/// </summary>
/// <remarks>
/// Safe for concurrent use: the connection's reader grows the windows while each stream's
/// writer takes from them. Once <see cref="StopUpdates"/> says the client can send no more
/// frames, a writer that finds no room is told so instead of waiting for ever.
/// </remarks>
internal sealed class Http2SendWindows
{
    /// <summary>The window every connection and stream starts with (RFC 9113 section 6.9.2).</summary>
    public const int DefaultInitialWindowSize = 65535;

    /// <summary>The largest a window may grow: 2^31-1 (RFC 9113 section 6.9.1).</summary>
    public const long MaxWindowSize = int.MaxValue;

    private readonly Lock gate = new();
    private readonly Dictionary<int, long> streamWindows = [];
    private long connectionWindow = DefaultInitialWindowSize;
    private long initialStreamWindow = DefaultInitialWindowSize;
    private bool updatesHaveStopped;
    private TaskCompletionSource changed = NewSignal();

    /// <summary>
    /// Opens <paramref name="streamId"/>'s window at the client's current initial window size.
    /// </summary>
    /// <param name="streamId">The stream.</param>
    public void OpenStream(int streamId)
    {
        lock (gate)
        {
            streamWindows[streamId] = initialStreamWindow;
        }
    }

    /// <summary>
    /// Forgets <paramref name="streamId"/>'s window; later updates for it are ignored.
    /// </summary>
    /// <param name="streamId">The stream.</param>
    public void CloseStream(int streamId)
    {
        lock (gate)
        {
            streamWindows.Remove(streamId);
        }
    }

    /// <summary>
    /// Grows the connection's window by a <c>WINDOW_UPDATE</c> on stream 0.
    /// </summary>
    /// <param name="increment">The increment, 1 to 2^31-1.</param>
    /// <returns><see langword="false"/> when the window would pass 2^31-1, a <c>FLOW_CONTROL_ERROR</c>.</returns>
    public bool GrowConnectionWindow(int increment)
    {
        lock (gate)
        {
            if (connectionWindow + increment > MaxWindowSize)
            {
                return false;
            }

            connectionWindow += increment;
            Signal();

            return true;
        }
    }

    /// <summary>
    /// Grows an open stream's window by a <c>WINDOW_UPDATE</c> on it; an update for a stream
    /// that is not open is ignored.
    /// </summary>
    /// <param name="streamId">The stream.</param>
    /// <param name="increment">The increment, 1 to 2^31-1.</param>
    /// <returns><see langword="false"/> when the window would pass 2^31-1, a <c>FLOW_CONTROL_ERROR</c>.</returns>
    public bool GrowStreamWindow(int streamId, int increment)
    {
        lock (gate)
        {
            if (!streamWindows.TryGetValue(streamId, out var window))
            {
                return true;
            }

            if (window + increment > MaxWindowSize)
            {
                return false;
            }

            streamWindows[streamId] = window + increment;
            Signal();

            return true;
        }
    }

    /// <summary>
    /// Applies a new <c>SETTINGS_INITIAL_WINDOW_SIZE</c>: every open stream's window moves by
    /// the difference, and may go below zero (RFC 9113 section 6.9.2).
    /// </summary>
    /// <param name="initialWindowSize">The new size, at most 2^31-1 (the frame parser refuses more).</param>
    /// <returns><see langword="false"/> when an open stream's window would pass 2^31-1, a <c>FLOW_CONTROL_ERROR</c>.</returns>
    public bool ChangeInitialStreamWindow(uint initialWindowSize)
    {
        lock (gate)
        {
            var difference = initialWindowSize - initialStreamWindow;
            if (streamWindows.Values.Any(window => window + difference > MaxWindowSize))
            {
                return false;
            }

            initialStreamWindow = initialWindowSize;
            foreach (var streamId in streamWindows.Keys.ToArray())
            {
                streamWindows[streamId] += difference;
            }

            Signal();

            return true;
        }
    }

    /// <summary>
    /// Says the client can send no more frames, so no window will grow again: every writer
    /// waiting for room is told there is none.
    /// </summary>
    public void StopUpdates()
    {
        lock (gate)
        {
            updatesHaveStopped = true;
            Signal();
        }
    }

    /// <summary>
    /// Takes room for up to <paramref name="wanted"/> bytes of <c>DATA</c> on an open stream from
    /// both its window and the connection's, waiting until both have some.
    /// </summary>
    /// <param name="streamId">The open stream.</param>
    /// <param name="wanted">The most bytes wanted, at least 1.</param>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>The bytes that may be sent, 1 to <paramref name="wanted"/>; 0 when there is no room and no update can come.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the wait off.</exception>
    public async Task<int> TakeAsync(int streamId, int wanted, CancellationToken cancellationToken)
    {
        while (true)
        {
            Task windowChanged;
            lock (gate)
            {
                var room = Math.Min(Math.Min(connectionWindow, streamWindows[streamId]), wanted);
                if (room > 0)
                {
                    connectionWindow -= room;
                    streamWindows[streamId] -= room;

                    return (int)room;
                }

                if (updatesHaveStopped)
                {
                    return 0;
                }

                windowChanged = changed.Task;
            }

            await windowChanged.WaitAsync(cancellationToken);
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void Signal()
    {
        changed.TrySetResult();
        changed = NewSignal();
    }
}
