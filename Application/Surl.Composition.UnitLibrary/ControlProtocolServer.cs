using Surl.Control;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The control listener's one server (ADR-0124 decision 7, BL-816): every connection its own serving engine
/// accepts is answered by the instance's <see cref="ControlEndpoint"/>. It is never registered beside the protocol
/// servers, so the HTTP server keeps the <c>http</c> scheme there.
/// </summary>
/// <param name="endpoint">The instance's control endpoint.</param>
internal sealed class ControlProtocolServer(ControlEndpoint endpoint) : IConnectionProtocolServer
{
    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = ["http"];

    /// <inheritdoc/>
    public Task ServeAsync(IConnection connection, ExchangeContext context) => endpoint.ServeAsync(connection, context);
}
