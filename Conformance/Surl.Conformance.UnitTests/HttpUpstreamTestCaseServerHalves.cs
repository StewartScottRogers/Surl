using System.Net;
using System.Text;
using Surl.Core;
using Surl.Networking;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Http;

namespace Surl.Conformance;

/// <summary>
/// Surl's HTTP server half of one upstream test case (ADR-0080 decision 2) on loopback, as
/// <c>Surl.Console</c> composes a server: <see cref="HttpUpstreamTestCaseServer"/> behind the
/// serving engine on <c>Surl.Networking</c>'s socket listeners, once in plaintext for
/// <c>%HTTPPORT</c> and once over TLS for <c>%HTTPSPORT</c>. The listeners bind before the case is
/// parsed, because the case's replies name the ports; <see cref="Play"/> then hands over the
/// case's reply before curl starts.
/// </summary>
/// <remarks>
/// The TLS listener is registered for the scheme <c>wss</c>, which the listener offers only
/// <c>http/1.1</c> by ALPN: upstream's test server for <c>https</c> is HTTP/1.1 behind a TLS
/// tunnel that negotiates no <c>h2</c>, and Surl's <c>https</c> listener offers <c>h2</c> first
/// (ADR-0081), which would take the exchange away from the HTTP/1.x server half.
/// </remarks>
internal sealed class HttpUpstreamTestCaseServerHalves : IAsyncDisposable
{
    private const string TlsScheme = "wss";

    private readonly CaseServer plaintext;
    private readonly CaseServer secured;
    private readonly CancellationTokenSource stop;
    private readonly Task<SurlExitCode> serving;
    private readonly NoteCollectingExchangeLogFactory log;

    private HttpUpstreamTestCaseServerHalves(
        CaseServer plaintext, CaseServer secured, CancellationTokenSource stop, Task<SurlExitCode> serving, NoteCollectingExchangeLogFactory log, IReadOnlyDictionary<string, int> ports)
    {
        this.plaintext = plaintext;
        this.secured = secured;
        this.stop = stop;
        this.serving = serving;
        this.log = log;
        HttpPort = ports["http"];
        HttpsPort = ports[TlsScheme];
    }

    /// <summary>The plaintext listener's port: <c>%HTTPPORT</c>.</summary>
    public int HttpPort { get; }

    /// <summary>The TLS listener's port: <c>%HTTPSPORT</c>.</summary>
    public int HttpsPort { get; }

    /// <summary>The notes the server half wrote to its exchange logs so far.</summary>
    public string Notes => log.Text;

    /// <summary>
    /// Binds both listeners on <c>127.0.0.1</c> and starts serving; until <see cref="Play"/>,
    /// a connection is closed with no bytes.
    /// </summary>
    public static async Task<HttpUpstreamTestCaseServerHalves> StartAsync(ServerTlsSettings tlsSettings, CancellationToken cancellationToken)
    {
        var plaintext = new CaseServer("http");
        var secured = new CaseServer(TlsScheme);
        var listeners = new BoundPortRecordingListenerFactory(new SocketListenerFactory(tlsSettings), 2);
        var log = new NoteCollectingExchangeLogFactory();
        var engine = new ServingEngine(listeners, [plaintext, secured], log, TimeProvider.System, TimeSpan.Zero);
        var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var serving = engine.ServeAsync([new ListenUrl("http", "127.0.0.1", 0), new ListenUrl(TlsScheme, "127.0.0.1", 0)], stop.Token);

        if (await Task.WhenAny(listeners.AllBound, serving) == serving)
        {
            stop.Dispose();
            throw new InvalidOperationException($"The serving engine ended with {await serving} before both listeners bound.");
        }

        return new HttpUpstreamTestCaseServerHalves(plaintext, secured, stop, serving, log, await listeners.AllBound);
    }

    /// <summary>
    /// Answers every later connection, on either listener, with <paramref name="replies"/>.
    /// </summary>
    public void Play(HttpUpstreamTestCaseReplies replies)
    {
        var server = new HttpUpstreamTestCaseServer(replies);
        plaintext.Current = server;
        secured.Current = server;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        await serving;
        stop.Dispose();
    }

    /// <summary>
    /// Registers one scheme for whichever case server <see cref="Play"/> set.
    /// </summary>
    private sealed class CaseServer(string scheme) : IConnectionProtocolServer
    {
        public volatile IConnectionProtocolServer? Current;

        public IReadOnlyList<string> Schemes { get; } = [scheme];

        public Task ServeAsync(IConnection connection, ExchangeContext context) =>
            Current?.ServeAsync(connection, context) ?? Task.CompletedTask;
    }

    /// <summary>
    /// Starts listeners through the socket factory and records each one's bound port by scheme.
    /// </summary>
    private sealed class BoundPortRecordingListenerFactory(IListenerFactory inner, int expectedListenerCount) : IListenerFactory
    {
        private readonly Dictionary<string, int> ports = new(StringComparer.Ordinal);
        private readonly TaskCompletionSource<IReadOnlyDictionary<string, int>> allBound = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IReadOnlyDictionary<string, int>> AllBound => allBound.Task;

        public async ValueTask<IConnectionListener> StartConnectionListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken)
        {
            var listener = await inner.StartConnectionListenerAsync(listenUrl, cancellationToken);
            lock (ports)
            {
                ports[listener.ListenUrl.Scheme] = listener.ListenUrl.BoundPort ?? listener.ListenUrl.Port;
                if (ports.Count == expectedListenerCount)
                {
                    allBound.SetResult(new Dictionary<string, int>(ports, StringComparer.Ordinal));
                }
            }

            return listener;
        }

        public ValueTask<IDatagramListener> StartDatagramListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The HTTP server half takes no datagrams.");
    }

    /// <summary>
    /// Keeps the notes every exchange writes, for the test log; bytes are not kept twice.
    /// </summary>
    private sealed class NoteCollectingExchangeLogFactory : IExchangeLogFactory
    {
        private readonly StringBuilder notes = new();

        public string Text
        {
            get
            {
                lock (notes)
                {
                    return notes.ToString();
                }
            }
        }

        public IExchangeLog Create(long exchangeId, EndPoint remoteEndPoint) => new Log(this, exchangeId);

        public void NoteOutsideExchange(string text) => Append($"* {text}");

        private void Append(string line)
        {
            lock (notes)
            {
                notes.AppendLine(line);
            }
        }

        private sealed class Log(NoteCollectingExchangeLogFactory owner, long exchangeId) : IExchangeLog
        {
            public void BytesReceived(ReadOnlySpan<byte> bytes)
            {
            }

            public void BytesSent(ReadOnlySpan<byte> bytes)
            {
            }

            public void Note(string text) => owner.Append($"* [{exchangeId}] {text}");
        }
    }
}
