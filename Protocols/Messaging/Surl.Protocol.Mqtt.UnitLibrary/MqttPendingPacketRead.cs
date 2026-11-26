namespace Surl.Protocol.Mqtt;

/// <summary>
/// One packet read that runs while the connection also sends: disposing it ends a read still
/// waiting and waits for it, so the connection is never closed under a read.
/// </summary>
internal sealed class MqttPendingPacketRead : IAsyncDisposable
{
    private readonly CancellationTokenSource cancellation;

    /// <summary>
    /// Starts reading the next packet.
    /// </summary>
    /// <param name="reader">The connection's packet reader, which nothing else may read until this is disposed.</param>
    /// <param name="stopped">Cuts the read off.</param>
    public MqttPendingPacketRead(MqttPacketReader reader, CancellationToken stopped)
    {
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(stopped);
        Result = reader.ReadPacketAsync(cancellation.Token).AsTask();
    }

    /// <summary>
    /// What the read answers; see <see cref="MqttPacketReader.ReadPacketAsync"/>.
    /// </summary>
    public Task<MqttPacketReadResult> Result { get; }

    /// <summary>
    /// Cancels the read if it is still waiting and waits for it to end, whatever it ends with.
    /// </summary>
    /// <returns>A task that completes when no read is running.</returns>
    public async ValueTask DisposeAsync()
    {
        await cancellation.CancelAsync();
        await ((Task)Result).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        cancellation.Dispose();
    }
}
