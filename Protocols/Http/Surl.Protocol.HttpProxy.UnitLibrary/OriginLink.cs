using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// The proxy's connection to one origin, with the reader its responses are read through,
/// kept while curl's next request names the same <c>host:port</c> (ADR-0088 decision 3).
/// </summary>
internal sealed class OriginLink : IAsyncDisposable
{
    /// <summary>
    /// Wraps an open origin connection.
    /// </summary>
    /// <param name="host">The host the connection was opened to.</param>
    /// <param name="port">The port the connection was opened to.</param>
    /// <param name="connection">The open connection.</param>
    public OriginLink(string host, int port, IConnection connection)
    {
        Host = host;
        Port = port;
        Connection = connection;
        Reader = new HttpConnectionReader(connection, maxRequestHeadBytes: 0);
    }

    /// <summary>The host the connection was opened to.</summary>
    public string Host { get; }

    /// <summary>The port the connection was opened to.</summary>
    public int Port { get; }

    /// <summary>The connection.</summary>
    public IConnection Connection { get; }

    /// <summary>Reads the origin's bytes, buffered across responses.</summary>
    public HttpConnectionReader Reader { get; }

    /// <summary>
    /// Whether this link goes to <paramref name="host"/> and <paramref name="port"/>, the host
    /// compared without regard to case.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <param name="port">The port.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public bool GoesTo(string host, int port) =>
        port == Port && string.Equals(host, Host, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the origin's next response head within <paramref name="maxHeadBytes"/>.
    /// </summary>
    /// <param name="maxHeadBytes">The most bytes the head may hold; 0 means no limit.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The head; or no head, with ClosedBeforeAnyByte set when the origin closed without sending
    /// a byte, or clear when what it sent is no response head.
    /// </returns>
    public async Task<(OriginResponseHead? Head, bool ClosedBeforeAnyByte)> ReadResponseHeadAsync(long maxHeadBytes, CancellationToken cancellationToken)
    {
        if (!await Reader.WaitForBytesAsync(cancellationToken))
        {
            return (null, true);
        }

        var remaining = maxHeadBytes == 0 ? Array.MaxLength : (int)Math.Min(maxHeadBytes, Array.MaxLength);
        var statusLine = await Reader.ReadLineAsync(remaining, cancellationToken);
        var status = statusLine is null ? null : OriginResponseHead.ParseStatusLine(statusLine);
        if (status is null)
        {
            return (null, false);
        }

        var fields = await ReadFieldsAsync(remaining - statusLine!.Length - 1, cancellationToken);

        return fields is null
            ? (null, false)
            : (new OriginResponseHead(status.Value.MinorVersion, status.Value.StatusCode, status.Value.ReasonPhrase, fields), false);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => Connection.DisposeAsync();

    // Reads field lines up to the empty line; null when a line is too long, malformed, or the
    // origin closed first.
    private async Task<List<HttpRequestField>?> ReadFieldsAsync(int remaining, CancellationToken cancellationToken)
    {
        var fields = new List<HttpRequestField>();
        while (true)
        {
            var line = remaining > 0 ? await Reader.ReadLineAsync(remaining, cancellationToken) : null;
            if (line is null or [] or [(byte)'\r'])
            {
                return line is null ? null : fields;
            }

            remaining -= line.Length + 1;
            var field = OriginResponseHead.ParseFieldLine(line);
            if (field is null)
            {
                return null;
            }

            fields.Add(field);
        }
    }
}
