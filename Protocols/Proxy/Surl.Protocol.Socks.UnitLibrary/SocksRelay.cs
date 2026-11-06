using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// Relays bytes both ways between curl and the origin after a granted request, unchanged and
/// unread (ADR-0121 decision 3's last point, as ADR-0088 decision 4's tunnel).
/// </summary>
/// <remarks>
/// When one side stops sending, the relay half-closes the other side's connection; it ends once
/// both have stopped. When either side fails (a reset, an abort), both connections are aborted,
/// which ends the other direction too.
/// </remarks>
internal static class SocksRelay
{
    private const int CopyBufferBytes = 16384;

    /// <summary>
    /// Relays until both sides have stopped sending, or either fails.
    /// </summary>
    /// <param name="client">curl's connection.</param>
    /// <param name="clientReader">curl's reader, which may hold bytes sent after the request.</param>
    /// <param name="origin">The origin's connection.</param>
    /// <param name="cancellationToken">Cancels the relay.</param>
    /// <returns><see langword="true"/> when both sides closed cleanly; <see langword="false"/> when one failed and both were aborted.</returns>
    public static async Task<bool> RelayAsync(IConnection client, SocksClientReader clientReader, IConnection origin, CancellationToken cancellationToken)
    {
        var both = new[] { client, origin };
        var relayed = await Task.WhenAll(
            RelayOneWayAsync(clientReader.ReadAsync, origin, both, cancellationToken),
            RelayOneWayAsync(origin.ReadAsync, client, both, cancellationToken));

        return relayed[0] && relayed[1];
    }

    /// <summary>
    /// Relays as <see cref="RelayAsync"/> does and logs how the relay to <paramref name="target"/> ended.
    /// </summary>
    /// <param name="client">curl's connection.</param>
    /// <param name="clientReader">curl's reader, which may hold bytes sent after the request.</param>
    /// <param name="origin">The origin's connection.</param>
    /// <param name="target">The origin as <c>host:port</c>, for the log.</param>
    /// <param name="log">The exchange's log.</param>
    /// <param name="cancellationToken">Cancels the relay.</param>
    /// <returns>A task that completes when the relay is over.</returns>
    public static async Task RelayAndLogAsync(
        IConnection client, SocksClientReader clientReader, IConnection origin, string target, IExchangeLog log, CancellationToken cancellationToken)
    {
        var closedCleanly = await RelayAsync(client, clientReader, origin, cancellationToken);
        log.Note(closedCleanly
            ? $"The relay to {target} closed."
            : $"The relay to {target} failed; both connections were aborted.");
    }

    private static async Task<bool> RelayOneWayAsync(
        Func<Memory<byte>, CancellationToken, ValueTask<int>> read, IConnection destination, IConnection[] both, CancellationToken cancellationToken)
    {
        await Task.Yield();
        var buffer = new byte[CopyBufferBytes];
        try
        {
            int count;
            while ((count = await read(buffer, cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            }

            await destination.CompleteWritesAsync(cancellationToken);

            return true;
        }
        catch (IOException)
        {
            foreach (var connection in both)
            {
                connection.Abort();
            }

            return false;
        }
    }
}
