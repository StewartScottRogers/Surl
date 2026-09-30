using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// An <see cref="IDataConnectionOpener"/> that replays a script of what curl does on the FTP data
/// connection and records every request, so an FTP server is tested with no network (ADR-0052,
/// decision 9). It opens nothing and constructs no transport type.
/// </summary>
/// <remarks>
/// Passive and active requests each take the next entry of their own script, in the order it
/// was written. A request with its script exhausted throws <see cref="DataConnectionFailure.Unavailable"/>,
/// as <see cref="RefusingDataConnectionOpener"/> does. What curl sends and expects on a data
/// connection, an early close and a TLS upgrade are scripted on the <see cref="InMemoryConnection"/>
/// handed out. It is meant for one test at a time and is not safe for concurrent calls.
/// </remarks>
public sealed class InMemoryDataConnections : IDataConnectionOpener
{
    private readonly Queue<ScriptedPassiveListener> passiveScript = new();
    private readonly Queue<ScriptedActiveConnection> activeScript = new();
    private readonly List<PassiveListenerRequest> passiveRequests = [];
    private readonly List<ActiveConnectionRequest> activeRequests = [];
    private readonly List<InMemoryPassiveDataListener> passiveListeners = [];

    /// <summary>
    /// Every <see cref="StartPassiveListenerAsync"/> call, in order.
    /// </summary>
    public IReadOnlyList<PassiveListenerRequest> PassiveRequests => passiveRequests;

    /// <summary>
    /// Every <see cref="ConnectActiveAsync"/> call, in order.
    /// </summary>
    public IReadOnlyList<ActiveConnectionRequest> ActiveRequests => activeRequests;

    /// <summary>
    /// Every listener <see cref="StartPassiveListenerAsync"/> handed out, in order, so a test can
    /// check its accept timeouts and that it was disposed.
    /// </summary>
    public IReadOnlyList<InMemoryPassiveDataListener> PassiveListeners => passiveListeners;

    /// <summary>
    /// Scripts the next passive request to start a listener announcing <paramref name="localEndPoint"/>.
    /// </summary>
    /// <param name="localEndPoint">The address and port the listener announces.</param>
    /// <param name="acceptedConnection">
    /// The connection curl opens to it; <see langword="null"/> when curl never connects.
    /// </param>
    /// <returns>This instance, so a script is written in one expression.</returns>
    public InMemoryDataConnections ScriptPassiveListener(IPEndPoint localEndPoint, IConnection? acceptedConnection)
    {
        ArgumentNullException.ThrowIfNull(localEndPoint);

        passiveScript.Enqueue(new ScriptedPassiveListener(localEndPoint, acceptedConnection, DataConnectionFailure.Unavailable));
        return this;
    }

    /// <summary>
    /// Scripts the next passive request to fail with <paramref name="failure"/>.
    /// </summary>
    /// <param name="failure">Why no listener could be started.</param>
    /// <returns>This instance, so a script is written in one expression.</returns>
    public InMemoryDataConnections ScriptPassiveFailure(DataConnectionFailure failure)
    {
        passiveScript.Enqueue(new ScriptedPassiveListener(null, null, failure));
        return this;
    }

    /// <summary>
    /// Scripts the next active request to hand out <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">The connection opened to curl.</param>
    /// <returns>This instance, so a script is written in one expression.</returns>
    public InMemoryDataConnections ScriptActiveConnection(IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        activeScript.Enqueue(new ScriptedActiveConnection(connection, DataConnectionFailure.Unavailable));
        return this;
    }

    /// <summary>
    /// Scripts the next active request to fail with <paramref name="failure"/>.
    /// </summary>
    /// <param name="failure">Why the connection could not be opened.</param>
    /// <returns>This instance, so a script is written in one expression.</returns>
    public InMemoryDataConnections ScriptActiveFailure(DataConnectionFailure failure)
    {
        activeScript.Enqueue(new ScriptedActiveConnection(null, failure));
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<IPassiveDataListener> StartPassiveListenerAsync(
        EndPoint controlLocal, EndPoint controlRemote, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(controlLocal);
        ArgumentNullException.ThrowIfNull(controlRemote);
        cancellationToken.ThrowIfCancellationRequested();
        passiveRequests.Add(new PassiveListenerRequest(controlLocal, controlRemote));

        var scripted = passiveScript.TryDequeue(out var next)
            ? next
            : new ScriptedPassiveListener(null, null, DataConnectionFailure.Unavailable);
        var localEndPoint = scripted.LocalEndPoint ?? throw Failed(scripted.Failure, "passive listener");
        var listener = new InMemoryPassiveDataListener(localEndPoint, scripted.AcceptedConnection);
        passiveListeners.Add(listener);
        return ValueTask.FromResult<IPassiveDataListener>(listener);
    }

    /// <inheritdoc/>
    public ValueTask<IConnection> ConnectActiveAsync(
        EndPoint controlRemote, IPEndPoint target, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(controlRemote);
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        activeRequests.Add(new ActiveConnectionRequest(controlRemote, target, timeout));

        var scripted = activeScript.TryDequeue(out var next)
            ? next
            : new ScriptedActiveConnection(null, DataConnectionFailure.Unavailable);
        var connection = scripted.Connection ?? throw Failed(scripted.Failure, $"active connection to {target}");
        return ValueTask.FromResult(connection);
    }

    private static DataConnectionException Failed(DataConnectionFailure failure, string what) =>
        new(failure, $"The scripted {what} failed: {failure}.");

    private sealed record ScriptedPassiveListener(
        IPEndPoint? LocalEndPoint, IConnection? AcceptedConnection, DataConnectionFailure Failure);

    private sealed record ScriptedActiveConnection(IConnection? Connection, DataConnectionFailure Failure);
}
