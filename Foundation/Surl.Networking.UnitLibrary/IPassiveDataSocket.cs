using System.Net;
using System.Net.Sockets;

namespace Surl.Networking;

/// <summary>
/// A listening socket for one passive FTP data connection.
/// </summary>
internal interface IPassiveDataSocket
{
    /// <summary>
    /// The address and port the socket is bound to.
    /// </summary>
    IPEndPoint LocalEndPoint { get; }

    /// <summary>
    /// Accepts one connection.
    /// </summary>
    /// <param name="cancellationToken">Cuts the accept off.</param>
    /// <returns>The accepted transport.</returns>
    /// <exception cref="SocketException">The accept failed.</exception>
    /// <exception cref="AcceptedSocketLostException">The connection was gone before its end points could be read.</exception>
    Task<DataTransport> AcceptAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops listening and releases the socket. Calling it twice is harmless.
    /// </summary>
    void Close();
}
