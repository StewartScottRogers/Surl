namespace Surl.Protocol.Abstractions;

/// <summary>
/// One <see cref="IOriginConnector.ConnectAsync"/> call recorded by <see cref="InMemoryOriginConnector"/>.
/// </summary>
/// <param name="Host">The host the proxy named.</param>
/// <param name="Port">The port the proxy named.</param>
/// <param name="Timeout">The timeout the proxy gave.</param>
public sealed record OriginConnectionRequest(string Host, int Port, TimeSpan Timeout);
