namespace Surl.Conformance;

/// <summary>
/// The bytes a client sent to <c>surl</c> through a recording relay, one entry per TCP connection in
/// the order the relay accepted them (ADR-0089 decision 4). Thread-safe: the relay records each
/// connection from its own task while others are still open.
/// </summary>
public sealed class ClientByteTranscript
{
    private readonly List<List<byte>> connections = [];
    private readonly Lock gate = new();

    /// <summary>
    /// Gets the number of connections begun so far.
    /// </summary>
    public int ConnectionCount
    {
        get
        {
            lock (gate)
            {
                return connections.Count;
            }
        }
    }

    /// <summary>
    /// Begins the next connection, with nothing recorded on it yet.
    /// </summary>
    /// <returns>The connection's index: 0 for the first connection accepted, 1 for the next, and so on.</returns>
    public int BeginConnection()
    {
        lock (gate)
        {
            connections.Add([]);
            return connections.Count - 1;
        }
    }

    /// <summary>
    /// Appends bytes the client sent on a connection, after everything recorded on it before.
    /// </summary>
    /// <param name="connection">The index <see cref="BeginConnection"/> gave.</param>
    /// <param name="bytes">The bytes, exactly as the client sent them.</param>
    /// <exception cref="ArgumentOutOfRangeException">No connection has that index.</exception>
    public void Record(int connection, ReadOnlySpan<byte> bytes)
    {
        lock (gate)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(connection);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(connection, connections.Count);
            connections[connection].AddRange(bytes);
        }
    }

    /// <summary>
    /// Gets a copy of every connection's bytes, in connection order, for
    /// <see cref="CurlPortSurlSide.ClientRequestBytes"/>.
    /// </summary>
    /// <returns>One array per connection.</returns>
    public IReadOnlyList<byte[]> ToConnections()
    {
        lock (gate)
        {
            return [.. connections.Select(bytes => bytes.ToArray())];
        }
    }
}
