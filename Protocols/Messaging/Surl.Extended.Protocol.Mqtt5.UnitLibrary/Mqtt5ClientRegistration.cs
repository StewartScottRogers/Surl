namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// One connection's hold on a client identifier in <see cref="Mqtt5BrokerState"/>: it is told
/// when a later <c>CONNECT</c> for the identifier takes its session over (<c>[MQTT-3.1.4-3]</c>),
/// and tells the connection that took over once it has ended.
/// </summary>
/// <param name="previousEnded">Completes when the connection this one took over from has ended;
/// already complete when it took over from none.</param>
internal sealed class Mqtt5ClientRegistration(Task previousEnded)
{
    private readonly CancellationTokenSource takeover = new();
    private readonly TaskCompletionSource ended = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completes when the connection this registration took the identifier over from has ended,
    /// its session and Will settled; already complete when it took over from none.
    /// </summary>
    public Task PreviousEnded { get; } = previousEnded;

    /// <summary>Cancelled when a later connection takes the identifier over.</summary>
    public CancellationToken TakenOverToken => takeover.Token;

    /// <summary>Whether a later connection has taken the identifier over.</summary>
    public bool IsTakenOver => takeover.IsCancellationRequested;

    /// <summary>Completes once this registration's connection has ended.</summary>
    public Task Ended => ended.Task;

    /// <summary>Tells the connection that a later one has taken its identifier over.</summary>
    public void TakeOver() => takeover.Cancel();

    /// <summary>Records that the connection has ended, so one that took it over may go on.</summary>
    public void End() => ended.TrySetResult();
}
