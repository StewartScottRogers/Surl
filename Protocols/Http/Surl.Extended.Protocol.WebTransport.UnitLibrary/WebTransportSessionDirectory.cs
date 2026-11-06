using Surl.Http3;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// One connection's WebTransport sessions as the streams that name them see them (ADR-0117 decision
/// 3; WTH3 4.6): <c>Surl.Http3</c> hands a stream over here when the session it names is not open.
/// </summary>
/// <remarks>
/// A stream naming a session whose <c>CONNECT</c> has not been answered yet is held, not read past
/// its header, up to <see cref="HeldStreamCapacity"/> at a time, until the session is settled or
/// <c>--head-timeout</c> passes: served when the session is accepted, else reset and stopped with
/// <c>WT_BUFFERED_STREAM_REJECTED</c>, as is a stream past the capacity or naming a request that is
/// no session. A stream naming a session that has ended is reset and stopped with
/// <c>WT_SESSION_GONE</c>.
/// </remarks>
/// <param name="context">The exchange: its clock, limits and log.</param>
internal sealed class WebTransportSessionDirectory(ExchangeContext context)
{
    /// <summary>How many streams may wait for their session at once on one connection.</summary>
    public const int HeldStreamCapacity = 16;

    private readonly Lock gate = new();
    private readonly Dictionary<ulong, TaskCompletionSource<WebTransportSession?>> settlements = [];
    private readonly HashSet<ulong> ended = [];
    private int held;
    private int open;

    /// <summary>
    /// Settles what the request stream <paramref name="sessionId"/> is: an accepted session, or
    /// (<see langword="null"/>) no session - a refused <c>CONNECT</c> or an ordinary request.
    /// Streams waiting for it are served or refused.
    /// </summary>
    /// <param name="sessionId">The request stream's ID.</param>
    /// <param name="session">The accepted session, or <see langword="null"/>.</param>
    public void Settle(ulong sessionId, WebTransportSession? session)
    {
        lock (gate)
        {
            Settlement(sessionId).TrySetResult(session);
        }
    }

    /// <summary>
    /// Takes a place for a new session under the connection's session limit (ADR-0117 decision 5);
    /// <see cref="End"/> gives it back.
    /// </summary>
    /// <param name="limit">The sessions the connection may hold at once.</param>
    /// <returns><see langword="false"/> when the connection holds <paramref name="limit"/> sessions already.</returns>
    public bool TryOpenSession(int limit)
    {
        lock (gate)
        {
            if (open >= limit)
            {
                return false;
            }

            open++;
            return true;
        }
    }

    /// <summary>
    /// Records that session <paramref name="sessionId"/>, which <see cref="TryOpenSession"/> let in,
    /// has ended: its place is free again, and streams naming it from now on get <c>WT_SESSION_GONE</c>.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    public void End(ulong sessionId)
    {
        lock (gate)
        {
            open--;
            ended.Add(sessionId);
            settlements.Remove(sessionId);
        }
    }

    /// <summary>Serves, holds or refuses a stream whose session <c>Surl.Http3</c> found no open session for.</summary>
    /// <param name="stream">The stream, read past its header.</param>
    /// <param name="cancellationToken">Cancelled when the connection ends, fails or shuts down.</param>
    /// <returns>A task that completes when the stream is dealt with.</returns>
    public async Task ServeStreamAsync(Http3SessionStream stream, CancellationToken cancellationToken)
    {
        var settlement = Hold(stream.SessionId, out var refusal);
        var session = settlement is null ? null : await WaitAsync(settlement, cancellationToken);
        if (session is null)
        {
            context.Log.Note($"Stream {stream.Id} names session {stream.SessionId}, which is not open; it was reset and stopped with 0x{refusal:x}.");
            await RefuseAsync(stream, refusal, cancellationToken);
            return;
        }

        await session.ServeStreamAsync(stream, cancellationToken);
    }

    private static async Task RefuseAsync(Http3SessionStream stream, ulong code, CancellationToken cancellationToken)
    {
        if (stream.Stream.IsBidirectional)
        {
            await stream.ResetAsync(code, cancellationToken);
        }

        await stream.StopSendingAsync(code, cancellationToken);
    }

    // Null when the stream is refused at once: its session ended, or too many streams wait.
    private Task<WebTransportSession?>? Hold(ulong sessionId, out ulong refusal)
    {
        lock (gate)
        {
            refusal = ended.Contains(sessionId) ? WebTransportHttp3Codes.SessionGone : WebTransportHttp3Codes.BufferedStreamRejected;
            if (refusal == WebTransportHttp3Codes.SessionGone || held >= HeldStreamCapacity)
            {
                return null;
            }

            held++;
            return Settlement(sessionId).Task;
        }
    }

    // The session once it is settled, or null when it is no session or --head-timeout passes first.
    private async Task<WebTransportSession?> WaitAsync(Task<WebTransportSession?> settlement, CancellationToken cancellationToken)
    {
        // Neither wait throws: the settlement is only ever given a result, and WhenAny never faults.
        using var timer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeout = Task.Delay(context.Limits.HeadTimeout, context.TimeProvider, timer.Token);
        var first = await Task.WhenAny(settlement, timeout);
        await timer.CancelAsync();
        lock (gate)
        {
            held--;
        }

        return first == settlement ? await settlement : null;
    }

    private TaskCompletionSource<WebTransportSession?> Settlement(ulong sessionId)
    {
        if (!settlements.TryGetValue(sessionId, out var settlement))
        {
            settlement = new TaskCompletionSource<WebTransportSession?>(TaskCreationOptions.RunContinuationsAsynchronously);
            settlements[sessionId] = settlement;
        }

        return settlement;
    }
}
