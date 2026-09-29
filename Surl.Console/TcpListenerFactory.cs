using System.Diagnostics.CodeAnalysis;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// The listener factory <c>surl</c> serves with until <c>Surl.Networking</c> has its own
/// (BL-055): connection listeners are <see cref="TcpConnectionListener"/>s, and datagram
/// listeners are refused, because <c>Surl.Networking</c> has no datagram listener yet.
/// </summary>
/// <remarks>
/// The serving engine never asks for a datagram listener: it refuses a listen URL whose
/// scheme belongs to a datagram server before any listener starts (BL-032), and
/// <c>surl</c> registers no datagram server.
/// </remarks>
internal sealed class TcpListenerFactory : IListenerFactory
{
    /// <inheritdoc/>
    [ExcludeFromCodeCoverage(Justification = "Binds sockets; covered by the integration test in Surl.Console.UnitTests.")]
    public async ValueTask<IConnectionListener> StartConnectionListenerAsync(
        ListenUrl listenUrl, CancellationToken cancellationToken) =>
        await TcpConnectionListener.StartAsync(listenUrl, cancellationToken);

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always: there is no datagram listener yet (BL-031, BL-055).</exception>
    public ValueTask<IDatagramListener> StartDatagramListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken) =>
        throw new NotSupportedException("surl has no datagram listener yet.");
}
