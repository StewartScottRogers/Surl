using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// A passive FTP data listener started by <see cref="IDataConnectionOpener.StartPassiveListenerAsync"/>
/// (ADR-0052, decision 9). It hands out one connection and is disposed once that connection is
/// taken, when another <c>EPSV</c>, <c>PASV</c>, <c>EPRT</c> or <c>PORT</c> replaces it, or when
/// the exchange ends.
/// </summary>
public interface IPassiveDataListener : IAsyncDisposable
{
    /// <summary>
    /// The address and port the server announces in its <c>229</c> and <c>227</c> replies.
    /// </summary>
    IPEndPoint LocalEndPoint { get; }

    /// <summary>
    /// Waits for the first connection from the peer's address; a connection from any other
    /// address is closed unseen and the wait goes on.
    /// </summary>
    /// <param name="timeout">How long to wait for the connection.</param>
    /// <param name="cancellationToken">The exchange's cancellation token.</param>
    /// <returns>The data connection.</returns>
    /// <exception cref="DataConnectionException">
    /// <see cref="DataConnectionFailure.TimedOut"/> when none arrives within <paramref name="timeout"/>.
    /// </exception>
    ValueTask<IConnection> AcceptAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
