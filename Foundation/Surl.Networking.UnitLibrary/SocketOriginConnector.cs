using System.Net;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// Opens a proxy's connections to origins over TCP sockets (ADR-0088, decision 2): resolves the
/// host with <see cref="Dns"/>, keeps only the addresses the <see cref="AllowedOriginRule"/>
/// admits - resolving before checking, so a name that resolves to a forbidden address cannot get
/// past the rule - and connects to them in the resolver's order until one accepts. Every origin
/// connection is a <see cref="StreamConnection"/> with no TLS of its own: a proxy passes TLS
/// through, never terminates it.
/// </summary>
/// <remarks>
/// The timeout covers resolution and every connect attempt together. When every admitted address
/// fails, the last failure is reported: <see cref="OriginConnectionFailure.Refused"/> for a
/// connection refused, <see cref="OriginConnectionFailure.Unreachable"/> for any other socket error.
/// </remarks>
public sealed class SocketOriginConnector : IOriginConnector
{
    private readonly AllowedOriginRule rule;
    private readonly TimeProvider timeProvider;
    private readonly IOriginSockets sockets;

    /// <summary>
    /// Creates the connector.
    /// </summary>
    /// <param name="rule">Which origins may be reached, built by the composition root.</param>
    /// <param name="timeProvider">Times the timeout and the lingering close; <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public SocketOriginConnector(AllowedOriginRule rule, TimeProvider? timeProvider = null)
        : this(rule, timeProvider ?? TimeProvider.System, new SocketOriginSockets())
    {
    }

    /// <summary>
    /// Creates the connector over the given resolution and socket calls.
    /// </summary>
    /// <param name="rule">Which origins may be reached.</param>
    /// <param name="timeProvider">Times the timeout and the lingering close.</param>
    /// <param name="sockets">Resolves names and connects sockets.</param>
    internal SocketOriginConnector(AllowedOriginRule rule, TimeProvider timeProvider, IOriginSockets sockets)
    {
        ArgumentNullException.ThrowIfNull(rule);

        this.rule = rule;
        this.timeProvider = timeProvider;
        this.sockets = sockets;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/> is not 1 to 65535.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the connect off.</exception>
    public async ValueTask<IConnection> ConnectAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, IPEndPoint.MinPort + 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, IPEndPoint.MaxPort);

        using var timeoutSource = new CancellationTokenSource(timeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            var admitted = AdmittedEndPoints(host, port, await ResolveAsync(host, linked.Token));

            return await ConnectFirstAsync(host, admitted, linked.Token);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new OriginConnectionException(
                OriginConnectionFailure.TimedOut, $"Origin {host}:{port} did not accept within {timeout}.");
        }
    }

    private async Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            var addresses = await sockets.ResolveAsync(host, cancellationToken);

            return addresses.Length > 0
                ? addresses
                : throw HostNotFound(host, "it has no addresses");
        }
        catch (SocketException exception)
        {
            throw HostNotFound(host, exception.Message);
        }
    }

    private IPEndPoint[] AdmittedEndPoints(string host, int port, IPAddress[] addresses)
    {
        var admitted = addresses
            .Select(address => new IPEndPoint(DataConnectionPeer.Normalize(address), port))
            .Distinct()
            .Where(rule.Admits)
            .ToArray();

        return admitted.Length > 0
            ? admitted
            : throw new OriginConnectionException(
                OriginConnectionFailure.NotAllowed,
                $"Origin {host}:{port} is not an allowed origin: the rule admits none of {string.Join(", ", addresses.Select(address => address.ToString()))}.");
    }

    private async Task<IConnection> ConnectFirstAsync(string host, IPEndPoint[] targets, CancellationToken cancellationToken)
    {
        OriginConnectionException? lastFailure = null;

        foreach (var target in targets)
        {
            try
            {
                return CreateConnection(await sockets.ConnectAsync(target, cancellationToken));
            }
            catch (SocketException exception)
            {
                lastFailure = new OriginConnectionException(
                    exception.SocketErrorCode == SocketError.ConnectionRefused ? OriginConnectionFailure.Refused : OriginConnectionFailure.Unreachable,
                    $"Origin {host} could not be reached at {target}: {exception.Message}");
            }
        }

        throw lastFailure!;
    }

    private static OriginConnectionException HostNotFound(string host, string reason) =>
        new(OriginConnectionFailure.HostNotFound, $"Origin {host} did not resolve: {reason}.");

    private StreamConnection CreateConnection(DataTransport transport) =>
        new(transport.Stream, transport.LocalEndPoint, transport.RemoteEndPoint, transport.Control, null, timeProvider);
}
