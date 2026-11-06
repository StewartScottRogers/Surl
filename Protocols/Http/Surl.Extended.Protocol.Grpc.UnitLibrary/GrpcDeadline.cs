namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// A call's deadline (ADR-0116 decision 5): a token cancelled when the <c>grpc-timeout</c> passes on
/// the injected <see cref="TimeProvider" />, or when the stream's own token is.
/// </summary>
internal sealed class GrpcDeadline : IDisposable
{
    /// <summary>
    /// The longest timeout a timer can hold; a longer <c>grpc-timeout</c> (up to 99999999 hours) is
    /// treated as no deadline, which it is in practice.
    /// </summary>
    internal static readonly TimeSpan LongestTimer = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    private readonly CancellationTokenSource timer;
    private readonly CancellationTokenSource linked;

    /// <summary>Starts the deadline now.</summary>
    /// <param name="timeout">The <c>grpc-timeout</c>; <see langword="null" /> means none.</param>
    /// <param name="timeProvider">The clock the deadline runs on.</param>
    /// <param name="streamToken">The stream's token, which also cancels <see cref="Token" />.</param>
    public GrpcDeadline(TimeSpan? timeout, TimeProvider timeProvider, CancellationToken streamToken)
    {
        timer = timeout is { } delay && delay <= LongestTimer
            ? new CancellationTokenSource(delay, timeProvider)
            : new CancellationTokenSource();
        if (timeout == TimeSpan.Zero)
        {
            timer.Cancel();
        }

        linked = CancellationTokenSource.CreateLinkedTokenSource(streamToken, timer.Token);
    }

    /// <summary>Cancelled when the deadline passes or the stream's token is cancelled.</summary>
    public CancellationToken Token => linked.Token;

    /// <inheritdoc />
    public void Dispose()
    {
        linked.Dispose();
        timer.Dispose();
    }
}
