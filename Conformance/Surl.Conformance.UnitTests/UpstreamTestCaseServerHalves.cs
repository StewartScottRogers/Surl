using System.Net;
using System.Text;
using Surl.Core;
using Surl.Networking;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Ftp;
using Surl.Protocol.Http;

namespace Surl.Conformance;

/// <summary>
/// Surl's server halves of one upstream test case (ADR-0080 decision 2) on loopback, as
/// <c>Surl.Console</c> composes servers: <see cref="HttpUpstreamTestCaseServer"/> and
/// <see cref="FtpUpstreamTestCaseServer"/> behind the serving engine on <c>Surl.Networking</c>'s
/// socket listeners and data connections - HTTP in plaintext for <c>%HTTPPORT</c> and over TLS
/// for <c>%HTTPSPORT</c>, FTP in plaintext for <c>%FTPPORT</c> and on <c>[::1]</c> for
/// <c>%FTP6PORT</c>, and FTP over implicit TLS for <c>%FTPSPORT</c>. The listeners bind before
/// the case is parsed, because the case's replies name the ports; <see cref="Play"/> then hands
/// over the case's reply before curl starts.
/// </summary>
/// <remarks>
/// <para>
/// The HTTP TLS listener is registered for the scheme <c>wss</c>, which the listener offers only
/// <c>http/1.1</c> by ALPN: upstream's test server for <c>https</c> is HTTP/1.1 behind a TLS
/// tunnel that negotiates no <c>h2</c>, and Surl's <c>https</c> listener offers <c>h2</c> first
/// (ADR-0081), which would take the exchange away from the HTTP/1.x server half.
/// </para>
/// <para>
/// FTP logins go to <see cref="AnonymousAuthenticationPolicy"/>, the accept-all policy decision 2
/// names: a case tests curl's login, not Surl's accounts.
/// </para>
/// </remarks>
internal sealed class UpstreamTestCaseServerHalves : IAsyncDisposable
{
    private const string HttpTlsScheme = "wss";
    private const string LoopbackIPv4 = "127.0.0.1";
    private const string LoopbackIPv6 = "::1";

    private readonly CaseServer http;
    private readonly CaseServer httpSecured;
    private readonly CaseServer ftp;
    private readonly CaseServer ftpSecured;
    private readonly CancellationTokenSource stop;
    private readonly Task<SurlExitCode> serving;
    private readonly NoteCollectingExchangeLogFactory log;

    private UpstreamTestCaseServerHalves(
        CaseServer[] servers, CancellationTokenSource stop, Task<SurlExitCode> serving, NoteCollectingExchangeLogFactory log, IReadOnlyDictionary<string, int> ports)
    {
        (http, httpSecured, ftp, ftpSecured) = (servers[0], servers[1], servers[2], servers[3]);
        this.stop = stop;
        this.serving = serving;
        this.log = log;
        HttpPort = ports[PortKey("http", LoopbackIPv4)];
        HttpsPort = ports[PortKey(HttpTlsScheme, LoopbackIPv4)];
        FtpPort = ports[PortKey("ftp", LoopbackIPv4)];
        FtpIPv6Port = ports[PortKey("ftp", LoopbackIPv6)];
        FtpsPort = ports[PortKey("ftps", LoopbackIPv4)];
    }

    /// <summary>The plaintext HTTP listener's port: <c>%HTTPPORT</c>.</summary>
    public int HttpPort { get; }

    /// <summary>The HTTP TLS listener's port: <c>%HTTPSPORT</c>.</summary>
    public int HttpsPort { get; }

    /// <summary>The FTP listener's port on <c>127.0.0.1</c>: <c>%FTPPORT</c>.</summary>
    public int FtpPort { get; }

    /// <summary>The FTP listener's port on <c>[::1]</c>: <c>%FTP6PORT</c>.</summary>
    public int FtpIPv6Port { get; }

    /// <summary>The implicit-TLS FTP listener's port: <c>%FTPSPORT</c>.</summary>
    public int FtpsPort { get; }

    /// <summary>The notes the server halves wrote to their exchange logs so far.</summary>
    public string Notes => log.Text;

    /// <summary>
    /// Binds every listener and starts serving; until <see cref="Play"/>, a connection is closed
    /// with no bytes.
    /// </summary>
    public static async Task<UpstreamTestCaseServerHalves> StartAsync(ServerTlsSettings tlsSettings, CancellationToken cancellationToken)
    {
        CaseServer[] servers = [new("http"), new(HttpTlsScheme), new("ftp"), new("ftps")];
        ListenUrl[] urls =
        [
            new("http", LoopbackIPv4, 0),
            new(HttpTlsScheme, LoopbackIPv4, 0),
            new("ftp", LoopbackIPv4, 0),
            new("ftp", LoopbackIPv6, 0),
            new("ftps", LoopbackIPv4, 0),
        ];
        var listeners = new BoundPortRecordingListenerFactory(new SocketListenerFactory(tlsSettings), urls.Length);
        var log = new NoteCollectingExchangeLogFactory();
        var engine = new ServingEngine(
            listeners, servers, log, TimeProvider.System, TimeSpan.Zero, ConnectionLimits.Default, new SocketDataConnectionOpener(tlsSettings, TimeProvider.System));
        var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var serving = engine.ServeAsync(urls, stop.Token);

        if (await Task.WhenAny(listeners.AllBound, serving) == serving)
        {
            stop.Dispose();
            throw new InvalidOperationException($"The serving engine ended with {await serving} before every listener bound.");
        }

        return new UpstreamTestCaseServerHalves(servers, stop, serving, log, await listeners.AllBound);
    }

    /// <summary>
    /// Answers every later connection with the case's replies: HTTP's on the HTTP listeners,
    /// FTP's on the FTP listeners.
    /// </summary>
    public void Play(HttpUpstreamTestCaseReplies httpReplies, FtpUpstreamTestCaseReplies ftpReplies)
    {
        var httpServer = new HttpUpstreamTestCaseServer(httpReplies);
        http.Current = httpServer;
        httpSecured.Current = httpServer;
        var ftpServer = new FtpUpstreamTestCaseServer(ftpReplies, new AnonymousAuthenticationPolicy());
        ftp.Current = ftpServer;
        ftpSecured.Current = ftpServer;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        await serving;
        stop.Dispose();
    }

    private static string PortKey(string scheme, string host) => $"{scheme} {host}";

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
    /// Starts listeners through the socket factory and records each one's bound port by scheme
    /// and host.
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
                ports[PortKey(listenUrl.Scheme, listenUrl.Host)] = listener.ListenUrl.BoundPort ?? listener.ListenUrl.Port;
                if (ports.Count == expectedListenerCount)
                {
                    allBound.SetResult(new Dictionary<string, int>(ports, StringComparer.Ordinal));
                }
            }

            return listener;
        }

        public ValueTask<IDatagramListener> StartDatagramListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The HTTP and FTP server halves take no datagrams.");
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
