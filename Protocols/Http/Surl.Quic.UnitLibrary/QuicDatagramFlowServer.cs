using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Quic;

/// <summary>
/// Serves one QUIC connection over one <see cref="IDatagramFlow"/> (ADR-0086 decision 4): it feeds
/// the flow's datagrams to a <see cref="QuicServerConnection"/>, sends what the connection returns,
/// acts on the connection's deadlines on the injected <see cref="TimeProvider"/>, and, once the
/// handshake is complete, hands the connection to an <see cref="IQuicApplicationProtocol"/>.
/// </summary>
/// <remarks>
/// The ALPN protocols agreed and the transport parameters sent are the
/// <see cref="QuicServerConnectionOptions"/>' own, so HTTP/3 and DNS over QUIC both ride this loop.
/// </remarks>
public sealed class QuicDatagramFlowServer
{
    private readonly QuicServerConnectionOptions options;
    private readonly IQuicApplicationProtocol applicationProtocol;
    private readonly TimeProvider timeProvider;
    private readonly RandomNumberGenerator random;

    /// <summary>Creates a server that hands each connection to <paramref name="applicationProtocol"/>.</summary>
    /// <param name="options">What each connection serves: certificate, ALPN protocols, timeouts and transport parameters.</param>
    /// <param name="applicationProtocol">The protocol each connection is handed to once its handshake is complete.</param>
    /// <param name="timeProvider">The clock the connection's deadlines are measured and waited for on.</param>
    /// <param name="random">The source of the server's connection IDs and TLS randomness.</param>
    public QuicDatagramFlowServer(
        QuicServerConnectionOptions options,
        IQuicApplicationProtocol applicationProtocol,
        TimeProvider timeProvider,
        RandomNumberGenerator random)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(applicationProtocol);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(random);
        this.options = options;
        this.applicationProtocol = applicationProtocol;
        this.timeProvider = timeProvider;
        this.random = random;
    }

    /// <summary>
    /// Serves the connection <paramref name="flow"/>'s first datagram starts, until it ends: closed
    /// by either side, discarded at the idle timeout, or by the application returning (which closes
    /// it with application error code 0).
    /// </summary>
    /// <param name="flow">The client's datagrams, starting with its first Initial.</param>
    /// <param name="cancellationToken">Stops serving; the application's token is cancelled with it.</param>
    /// <returns>A task that completes when the connection has ended and the application has returned; an exception the application threw is thrown from it.</returns>
    public Task ServeAsync(IDatagramFlow flow, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(flow);
        return ServeFlowAsync(flow, cancellationToken);
    }

    /// <summary>Runs the loop, then always ends the application's side and waits for it, and only then throws what the loop threw.</summary>
    private async Task ServeFlowAsync(IDatagramFlow flow, CancellationToken cancellationToken)
    {
        var connection = new QuicServerConnection(options, timeProvider, random);
        var ending = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var loop = new QuicDatagramFlowLoop(
            new QuicApplicationConnection(connection, flow, ending), flow, applicationProtocol, timeProvider);
        ExceptionDispatchInfo? loopFailure = null;
        try
        {
            await loop.RunAsync(ending.Token);
        }
        catch (Exception exception)
        {
            loopFailure = ExceptionDispatchInfo.Capture(exception);
        }

        await loop.EndAsync();
        ending.Dispose();
        connection.Dispose();
        loopFailure?.Throw();
    }
}
