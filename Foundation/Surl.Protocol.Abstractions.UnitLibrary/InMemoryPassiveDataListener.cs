using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// An <see cref="IPassiveDataListener"/> that hands out one scripted connection, so an FTP server
/// is tested with no network (ADR-0052, decision 9). <see cref="InMemoryDataConnections"/> creates
/// one for each scripted passive listener. It binds nothing and constructs no transport type.
/// </summary>
/// <param name="localEndPoint">The address and port the listener announces.</param>
/// <param name="acceptedConnection">
/// The connection the first <see cref="AcceptAsync"/> hands out, standing in for curl connecting;
/// <see langword="null"/> when curl never connects, so every accept times out.
/// </param>
public sealed class InMemoryPassiveDataListener(IPEndPoint localEndPoint, IConnection? acceptedConnection)
    : IPassiveDataListener
{
    private readonly List<TimeSpan> acceptTimeouts = [];
    private IConnection? connectionToHandOut = acceptedConnection;

    /// <inheritdoc/>
    public IPEndPoint LocalEndPoint { get; } = localEndPoint ?? throw new ArgumentNullException(nameof(localEndPoint));

    /// <summary>
    /// The timeout of every <see cref="AcceptAsync"/> call, in order.
    /// </summary>
    public IReadOnlyList<TimeSpan> AcceptTimeouts => acceptTimeouts;

    /// <summary>
    /// Whether <see cref="DisposeAsync"/> has been called.
    /// </summary>
    public bool Disposed { get; private set; }

    /// <inheritdoc/>
    /// <remarks>
    /// Hands out the scripted connection once; every later call, and every call when none was
    /// scripted, throws <see cref="DataConnectionFailure.TimedOut"/>.
    /// </remarks>
    public ValueTask<IConnection> AcceptAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Disposed, this);
        acceptTimeouts.Add(timeout);

        var connection = connectionToHandOut
            ?? throw new DataConnectionException(
                DataConnectionFailure.TimedOut, $"No data connection arrived within {timeout}.");
        connectionToHandOut = null;
        return ValueTask.FromResult(connection);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
