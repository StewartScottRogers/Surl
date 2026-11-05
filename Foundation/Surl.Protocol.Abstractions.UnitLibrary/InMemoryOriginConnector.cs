namespace Surl.Protocol.Abstractions;

/// <summary>
/// An <see cref="IOriginConnector"/> that hands out connections and failures a test scripted per
/// origin and records every request, so a proxy server is tested with no network (ADR-0088,
/// decision 2). It resolves no name, applies no allowed-origin rule and constructs no transport
/// type: a refused origin is scripted as <see cref="OriginConnectionFailure.NotAllowed"/>.
/// </summary>
/// <remarks>
/// Each origin, a host compared without regard to case and a port, takes the next entry of its
/// own script, in the order it was written. A request for an origin with no script left is
/// <see cref="OriginConnectionFailure.Refused"/>, as an address nobody listens on is. It is meant
/// for one test at a time and is not safe for concurrent calls.
/// </remarks>
public sealed class InMemoryOriginConnector : IOriginConnector
{
    private readonly Dictionary<(string Host, int Port), Queue<ScriptedOrigin>> scripts = [];
    private readonly List<OriginConnectionRequest> requests = [];

    /// <summary>
    /// Every <see cref="ConnectAsync"/> call, in order.
    /// </summary>
    public IReadOnlyList<OriginConnectionRequest> Requests => requests;

    /// <summary>
    /// Scripts the next request for <paramref name="host"/> and <paramref name="port"/> to hand
    /// out <paramref name="connection"/>.
    /// </summary>
    /// <param name="host">The host the proxy will name.</param>
    /// <param name="port">The port the proxy will name.</param>
    /// <param name="connection">The connection to the origin; the test plays the origin's end.</param>
    /// <returns>This instance, so a script is written in one expression.</returns>
    public InMemoryOriginConnector ScriptConnection(string host, int port, IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return Script(host, port, new ScriptedOrigin(connection, OriginConnectionFailure.Refused));
    }

    /// <summary>
    /// Scripts the next request for <paramref name="host"/> and <paramref name="port"/> to fail
    /// with <paramref name="failure"/>.
    /// </summary>
    /// <param name="host">The host the proxy will name.</param>
    /// <param name="port">The port the proxy will name.</param>
    /// <param name="failure">Why the connection cannot be opened.</param>
    /// <returns>This instance, so a script is written in one expression.</returns>
    public InMemoryOriginConnector ScriptFailure(string host, int port, OriginConnectionFailure failure) =>
        Script(host, port, new ScriptedOrigin(null, failure));

    /// <inheritdoc/>
    public ValueTask<IConnection> ConnectAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);
        cancellationToken.ThrowIfCancellationRequested();
        requests.Add(new OriginConnectionRequest(host, port, timeout));

        var scripted = scripts.TryGetValue(Key(host, port), out var script) && script.TryDequeue(out var next)
            ? next
            : new ScriptedOrigin(null, OriginConnectionFailure.Refused);
        var connection = scripted.Connection
            ?? throw new OriginConnectionException(scripted.Failure, $"The scripted connection to {host}:{port} failed: {scripted.Failure}.");

        return ValueTask.FromResult(connection);
    }

    private InMemoryOriginConnector Script(string host, int port, ScriptedOrigin scripted)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);

        var key = Key(host, port);

        if (!scripts.TryGetValue(key, out var script))
        {
            script = new Queue<ScriptedOrigin>();
            scripts.Add(key, script);
        }

        script.Enqueue(scripted);
        return this;
    }

    private static (string Host, int Port) Key(string host, int port) => (host.ToLowerInvariant(), port);

    private sealed record ScriptedOrigin(IConnection? Connection, OriginConnectionFailure Failure);
}
