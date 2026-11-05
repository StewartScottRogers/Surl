using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.HttpServing;

/// <summary>
/// Takes a connection over from HTTP/1.1 after an upgrade: <see cref="Http1ConnectionServer"/>
/// writes the <see cref="HttpAnswer"/>'s head - a <c>101 Switching Protocols</c> - then awaits
/// <see cref="TakeOverAsync"/>, which owns the connection until it ends; nothing is drained or
/// written after it (ADR-0084, decision 1).
/// </summary>
public interface IHttpConnectionTakeover
{
    /// <summary>
    /// Serves the connection in the upgraded protocol until the exchange is over.
    /// </summary>
    /// <param name="connection">The connection, with the answer's head already written to it.</param>
    /// <param name="reader">
    /// The connection's reader, which may hold bytes the client sent after the request head;
    /// read through it, never around it.
    /// </param>
    /// <param name="cancellationToken">The exchange's cancellation.</param>
    /// <returns>A task that completes when the upgraded protocol is done with the connection.</returns>
    Task TakeOverAsync(IConnection connection, HttpConnectionReader reader, CancellationToken cancellationToken);
}
