using System.Net;

namespace Surl.Networking;

/// <summary>
/// Resolves origin names with <see cref="Dns"/> and connects TCP sockets to origins for
/// <see cref="SocketOriginConnector"/>. The connect is the one FTP's active data connection
/// makes (<see cref="SocketDataConnectionSockets.ConnectAsync"/>).
/// </summary>
internal sealed class SocketOriginSockets : IOriginSockets
{
    private readonly SocketDataConnectionSockets sockets = new();

    /// <inheritdoc/>
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) =>
        Dns.GetHostAddressesAsync(host, cancellationToken);

    /// <inheritdoc/>
    public Task<DataTransport> ConnectAsync(IPEndPoint target, CancellationToken cancellationToken) =>
        sockets.ConnectAsync(target, cancellationToken);
}
