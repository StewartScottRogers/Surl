using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// Relays bytes both ways between curl and an origin, unchanged and unread: a <c>CONNECT</c>
/// tunnel, or a connection after a relayed <c>101</c> (ADR-0088 decisions 3 and 4).
/// </summary>
/// <remarks>
/// When one side stops sending, the proxy half-closes the other side's connection; the relay
/// ends once both have stopped. When either side fails (a reset, an abort), both connections
/// are aborted, which ends the other direction too.
/// </remarks>
internal static class ProxyTunnel
{
    /// <summary>
    /// Relays until both sides have stopped sending, or either fails.
    /// </summary>
    /// <param name="client">curl's connection.</param>
    /// <param name="clientReader">curl's reader, which may hold bytes already received.</param>
    /// <param name="origin">The origin's link, whose reader may hold bytes already received.</param>
    /// <param name="cancellationToken">Cancels the relay.</param>
    /// <returns><see langword="true"/> when both sides closed cleanly; <see langword="false"/> when one failed and both were aborted.</returns>
    public static async Task<bool> RelayAsync(IConnection client, HttpConnectionReader clientReader, OriginLink origin, CancellationToken cancellationToken)
    {
        var both = new[] { client, origin.Connection };
        var relayed = await Task.WhenAll(
            RelayOneWayAsync(clientReader, origin.Connection, both, cancellationToken),
            RelayOneWayAsync(origin.Reader, client, both, cancellationToken));

        return relayed[0] && relayed[1];
    }

    private static async Task<bool> RelayOneWayAsync(HttpConnectionReader source, IConnection destination, IConnection[] both, CancellationToken cancellationToken)
    {
        await Task.Yield();
        try
        {
            await ProxyBodyCopier.CopyUntilClosedAsync(source, destination, cancellationToken);
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
