using System.Net;
using System.Text;
using Surl.Content;
using Surl.Core;
using Surl.Extended.Protocol.DnsQuery;
using Surl.LineProtocol;
using Surl.Networking;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Dict;
using Surl.Protocol.Ftp;
using Surl.Protocol.Gopher;
using Surl.Protocol.Http;
using Surl.Protocol.HttpProxy;
using Surl.Protocol.Imap;
using Surl.Protocol.Mqtt;
using Surl.Protocol.Pop3;
using Surl.Protocol.Smtp;
using Surl.Protocol.Socks;
using Surl.Protocol.Telnet;
using Surl.Protocol.Tftp;

namespace Surl.Conformance;

/// <summary>
/// Surl's server halves of one upstream test case (ADR-0080 decision 2) on loopback, as
/// <c>Surl.Console</c> composes servers, behind the serving engine on <c>Surl.Networking</c>'s
/// socket listeners and data connections: <see cref="HttpUpstreamTestCaseServer"/> in plaintext
/// for <c>%HTTPPORT</c> and over TLS for <c>%HTTPSPORT</c>; <see cref="FtpUpstreamTestCaseServer"/>
/// for <c>%FTPPORT</c>, on <c>[::1]</c> for <c>%FTP6PORT</c> and over implicit TLS for
/// <c>%FTPSPORT</c>; <see cref="GopherUpstreamTestCaseServer"/> for <c>%GOPHERPORT</c>, on
/// <c>[::1]</c> for <c>%GOPHER6PORT</c> and over TLS for <c>%GOPHERSPORT</c>;
/// <see cref="MqttUpstreamTestCaseServer"/> for <c>%MQTTPORT</c> and over TLS for
/// <c>%MQTTSPORT</c>; <see cref="TftpUpstreamTestCaseServer"/> on a datagram listener for
/// <c>%TFTPPORT</c>; and Surl's own <see cref="DictProtocolServer"/> (no definitions) and
/// <see cref="TelnetProtocolServer"/> for <c>%DICTPORT</c> and <c>%TELNETPORT</c>, whose cases
/// give their servers nothing to send; and the file-system servers
/// <see cref="UpstreamTestCaseFileServers"/> makes, SSH for <c>%SSHPORT</c> (SCP and SFTP) and SMB
/// for <c>%SMBPORT</c>; and <see cref="ImapUpstreamTestCaseServer"/>,
/// <see cref="Pop3UpstreamTestCaseServer"/> and <see cref="SmtpUpstreamTestCaseServer"/> in
/// plaintext on <c>127.0.0.1</c> and <c>[::1]</c> for <c>%IMAPPORT</c>, <c>%POP3PORT</c>,
/// <c>%SMTPPORT</c> and their <c>6</c> forms; and, for an <c>http-unix</c> case, the plaintext HTTP
/// server half on a Unix domain socket file for <c>%HTTPUNIXPATH</c>. The listeners bind before
/// the case is parsed, because the case's replies name the ports; <see cref="Play"/> then hands over the case's reply before
/// curl starts.
/// </summary>
/// <remarks>
/// <para>
/// The HTTP TLS listener is registered for the scheme <c>wss</c>, which the listener offers only
/// <c>http/1.1</c> by ALPN: upstream's test server for <c>https</c> is HTTP/1.1 behind a TLS
/// tunnel that negotiates no <c>h2</c>, and Surl's <c>https</c> listener offers <c>h2</c> first
/// (ADR-0081), which would take the exchange away from the HTTP/1.x server half.
/// </para>
/// <para>
/// FTP, MQTT, IMAP, POP3 and SMTP logins go to <see cref="AnonymousAuthenticationPolicy"/>, the
/// accept-all policy decision 2 names: a case tests curl's login, not Surl's accounts.
/// </para>
/// </remarks>
internal sealed class UpstreamTestCaseServerHalves : IAsyncDisposable
{
    private const string HttpTlsScheme = "wss";

    // The plaintext proxy needs a scheme of its own because "http" is the origin's; the HTTPS proxy
    // is the "https" scheme with ListenUrl.IsProxy, which the HTTP TLS half (on "wss") leaves free.
    private const string HttpProxyScheme = "http-proxy";
    private const string HttpsProxyScheme = "https";
    private const string LoopbackIPv4 = "127.0.0.1";
    private const string LoopbackIPv6 = "::1";

    // Each case server's scheme and the hosts its listeners bind, in Play's order.
    private static readonly (string Scheme, string[] Hosts)[] Listeners =
    [
        ("http", [LoopbackIPv4]),
        (HttpTlsScheme, [LoopbackIPv4]),
        ("ftp", [LoopbackIPv4, LoopbackIPv6]),
        ("ftps", [LoopbackIPv4]),
        ("gopher", [LoopbackIPv4, LoopbackIPv6]),
        ("gophers", [LoopbackIPv4]),
        ("mqtt", [LoopbackIPv4]),
        ("mqtts", [LoopbackIPv4]),
        ("dict", [LoopbackIPv4]),
        ("telnet", [LoopbackIPv4]),
        ("sftp", [LoopbackIPv4]),
        ("smb", [LoopbackIPv4]),
        ("imap", [LoopbackIPv4, LoopbackIPv6]),
        ("pop3", [LoopbackIPv4, LoopbackIPv6]),
        ("smtp", [LoopbackIPv4, LoopbackIPv6]),
        (HttpProxyScheme, [LoopbackIPv4]),
        (HttpsProxyScheme, [LoopbackIPv4]),
        ("socks4", [LoopbackIPv4]),
        ("socks5", [LoopbackIPv4]),
    ];

    private readonly CaseServer[] servers;
    private readonly CaseDatagramServer tftp;
    private readonly CaseDatagramServer dns;
    private readonly CancellationTokenSource stop;
    private readonly Task<SurlExitCode> serving;
    private readonly NoteCollectingExchangeLogFactory log;
    private readonly IReadOnlyDictionary<string, int> ports;

    private UpstreamTestCaseServerHalves(
        CaseServer[] servers, CaseDatagramServer tftp, CaseDatagramServer dns, CancellationTokenSource stop, Task<SurlExitCode> serving, NoteCollectingExchangeLogFactory log, IReadOnlyDictionary<string, int> ports)
    {
        this.servers = servers;
        this.tftp = tftp;
        this.dns = dns;
        this.stop = stop;
        this.serving = serving;
        this.log = log;
        this.ports = ports;
    }

    /// <summary>The plaintext HTTP listener's port: <c>%HTTPPORT</c>.</summary>
    public int HttpPort => ports[PortKey("http", LoopbackIPv4)];

    /// <summary>The HTTP TLS listener's port: <c>%HTTPSPORT</c>.</summary>
    public int HttpsPort => ports[PortKey(HttpTlsScheme, LoopbackIPv4)];

    /// <summary>The FTP listener's port on <c>127.0.0.1</c>: <c>%FTPPORT</c>.</summary>
    public int FtpPort => ports[PortKey("ftp", LoopbackIPv4)];

    /// <summary>The FTP listener's port on <c>[::1]</c>: <c>%FTP6PORT</c>.</summary>
    public int FtpIPv6Port => ports[PortKey("ftp", LoopbackIPv6)];

    /// <summary>The implicit-TLS FTP listener's port: <c>%FTPSPORT</c>.</summary>
    public int FtpsPort => ports[PortKey("ftps", LoopbackIPv4)];

    /// <summary>The Gopher listener's port on <c>127.0.0.1</c>: <c>%GOPHERPORT</c>.</summary>
    public int GopherPort => ports[PortKey("gopher", LoopbackIPv4)];

    /// <summary>The Gopher listener's port on <c>[::1]</c>: <c>%GOPHER6PORT</c>.</summary>
    public int GopherIPv6Port => ports[PortKey("gopher", LoopbackIPv6)];

    /// <summary>The Gopher TLS listener's port: <c>%GOPHERSPORT</c>.</summary>
    public int GophersPort => ports[PortKey("gophers", LoopbackIPv4)];

    /// <summary>The MQTT listener's port: <c>%MQTTPORT</c>.</summary>
    public int MqttPort => ports[PortKey("mqtt", LoopbackIPv4)];

    /// <summary>The MQTT TLS listener's port: <c>%MQTTSPORT</c>.</summary>
    public int MqttsPort => ports[PortKey("mqtts", LoopbackIPv4)];

    /// <summary>The DICT listener's port: <c>%DICTPORT</c>.</summary>
    public int DictPort => ports[PortKey("dict", LoopbackIPv4)];

    /// <summary>The TELNET listener's port: <c>%TELNETPORT</c>.</summary>
    public int TelnetPort => ports[PortKey("telnet", LoopbackIPv4)];

    /// <summary>The SSH listener's port, for SCP and SFTP: <c>%SSHPORT</c>.</summary>
    public int SshPort => ports[PortKey("sftp", LoopbackIPv4)];

    /// <summary>The SMB listener's port: <c>%SMBPORT</c>.</summary>
    public int SmbPort => ports[PortKey("smb", LoopbackIPv4)];

    /// <summary>The TFTP datagram listener's port: <c>%TFTPPORT</c>.</summary>
    public int TftpPort => ports[PortKey("tftp", LoopbackIPv4)];

    /// <summary>The DNS datagram listener's port: <c>%DNSPORT</c>, which a case's <c>CURL_DNS_SERVER</c> names (ADR-0123 decision 4).</summary>
    public int DnsPort => ports[PortKey("dns", LoopbackIPv4)];

    /// <summary>The IMAP listener's port on <c>127.0.0.1</c>: <c>%IMAPPORT</c>.</summary>
    public int ImapPort => ports[PortKey("imap", LoopbackIPv4)];

    /// <summary>The IMAP listener's port on <c>[::1]</c>: <c>%IMAP6PORT</c>.</summary>
    public int ImapIPv6Port => ports[PortKey("imap", LoopbackIPv6)];

    /// <summary>The POP3 listener's port on <c>127.0.0.1</c>: <c>%POP3PORT</c>.</summary>
    public int Pop3Port => ports[PortKey("pop3", LoopbackIPv4)];

    /// <summary>The POP3 listener's port on <c>[::1]</c>: <c>%POP36PORT</c>.</summary>
    public int Pop3IPv6Port => ports[PortKey("pop3", LoopbackIPv6)];

    /// <summary>The SMTP listener's port on <c>127.0.0.1</c>: <c>%SMTPPORT</c>.</summary>
    public int SmtpPort => ports[PortKey("smtp", LoopbackIPv4)];

    /// <summary>The SMTP listener's port on <c>[::1]</c>: <c>%SMTP6PORT</c>.</summary>
    public int SmtpIPv6Port => ports[PortKey("smtp", LoopbackIPv6)];

    /// <summary>The plaintext HTTP proxy listener's port: <c>%PROXYPORT</c>.</summary>
    public int ProxyPort => ports[PortKey(HttpProxyScheme, LoopbackIPv4)];

    /// <summary>The HTTPS proxy listener's port: <c>%HTTPSPROXYPORT</c>.</summary>
    public int HttpsProxyPort => ports[PortKey(HttpsProxyScheme, LoopbackIPv4)];

    /// <summary>The SOCKS4 listener's port: <c>%SOCKSPORT</c> for a case naming <c>socks4</c>.</summary>
    public int Socks4Port => ports[PortKey("socks4", LoopbackIPv4)];

    /// <summary>The SOCKS5 listener's port: <c>%SOCKSPORT</c> for any other case naming a SOCKS server.</summary>
    public int Socks5Port => ports[PortKey("socks5", LoopbackIPv4)];

    /// <summary>The notes the server halves wrote to their exchange logs so far.</summary>
    public string Notes => log.Text;

    /// <summary>
    /// Waits, up to <paramref name="longest"/>, for every exchange opened so far to end, so the
    /// server halves have read what curl sent before it exited - its last <c>DISCONNECT</c> or
    /// <c>QUIT</c> - before their logs are checked. An exchange still open then is left to run.
    /// </summary>
    public async Task WaitForExchangesToEndAsync(TimeSpan longest, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + longest;
        while (log.HasOpenExchanges && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }
    }

    /// <summary>
    /// Binds every listener and starts serving; until <see cref="Play"/>, a connection is closed
    /// with no bytes and a datagram is not answered.
    /// </summary>
    /// <param name="tlsSettings">The certificate the TLS listeners serve.</param>
    /// <param name="httpUnixSocketPath">
    /// The socket file the plaintext HTTP server half also listens on, for an <c>http-unix</c>
    /// case's <c>%HTTPUNIXPATH</c>, or <see langword="null"/> for none.
    /// </param>
    /// <param name="socksUnixSocketPath">The socket file the SOCKS5 server half also listens on, for a <c>socks5unix</c> case's <c>%SOCKSUNIXPATH</c>, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Stops the serving engine.</param>
    public static async Task<UpstreamTestCaseServerHalves> StartAsync(
        ServerTlsSettings tlsSettings, string? httpUnixSocketPath, string? socksUnixSocketPath, CancellationToken cancellationToken)
    {
        CaseServer[] servers = [.. Listeners.Select(listener => new CaseServer(listener.Scheme))];
        var tftp = new CaseDatagramServer("tftp");
        var dns = new CaseDatagramServer("dns");
        ListenUrl[] urls =
        [
            .. Listeners.SelectMany(listener => listener.Hosts.Select(host => ListenUrlFor(listener.Scheme, host))),
            new("tftp", LoopbackIPv4, 0),
            new("dns", LoopbackIPv4, 0),
            .. HttpUnixSocketListenUrls(httpUnixSocketPath),
            .. SocksUnixSocketListenUrls(socksUnixSocketPath),
        ];
        var listeners = new BoundPortRecordingListenerFactory(new SocketListenerFactory(tlsSettings), urls.Length);
        var log = new NoteCollectingExchangeLogFactory();
        var engine = new ServingEngine(
            listeners, [.. servers, tftp, dns], log, TimeProvider.System, TimeSpan.Zero, ConnectionLimits.Default, new SocketDataConnectionOpener(tlsSettings, TimeProvider.System));
        var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var serving = engine.ServeAsync(urls, stop.Token);

        if (await Task.WhenAny(listeners.AllBound, serving) == serving)
        {
            stop.Dispose();
            throw new InvalidOperationException($"The serving engine ended with {await serving} before every listener bound.");
        }

        return new UpstreamTestCaseServerHalves(servers, tftp, dns, stop, serving, log, await listeners.AllBound);
    }

    /// <summary>
    /// Answers every later connection and flow with the case's replies, each protocol's on its
    /// own listeners.
    /// </summary>
    public void Play(
        HttpUpstreamTestCaseReplies httpReplies,
        FtpUpstreamTestCaseReplies ftpReplies,
        GopherUpstreamTestCaseReplies gopherReplies,
        MqttUpstreamTestCaseReplies mqttReplies,
        TftpUpstreamTestCaseReplies tftpReplies,
        IConnectionProtocolServer ssh,
        IConnectionProtocolServer smb,
        MailUpstreamTestCaseReplies mailReplies,
        HttpProxyUpstreamTestCaseReplies proxyReplies,
        SocksUpstreamTestCaseReplies socksReplies,
        DnsQueryUpstreamTestCaseReplies dnsReplies,
        ArrivalOrderedReceivedLog arrivalLog)
    {
        // Upstream's sws sends every tunnel to 127.0.0.1 at the CONNECT's port (BL-556), so the proxy
        // reaches real loopback sockets, whatever host the request names.
        var proxy = new HttpProxyUpstreamTestCaseServer(proxyReplies, new SocketOriginConnector(AllowedOriginRule.LoopbackOnly));
        var socks = new SocksUpstreamTestCaseServer(socksReplies, new SocketOriginConnector(AllowedOriginRule.LoopbackOnly));
        var http = new HttpUpstreamTestCaseServer(httpReplies);
        var ftp = new FtpUpstreamTestCaseServer(ftpReplies, new AnonymousAuthenticationPolicy());
        var gopher = new GopherUpstreamTestCaseServer(gopherReplies);
        var mqtt = new MqttUpstreamTestCaseServer(mqttReplies, new AnonymousAuthenticationPolicy());
        var noDefinitions = new ContentStore(
            InMemoryContentFileSystem.RootPath, new InMemoryContentFileSystem(TimeProvider.System), new ContentExposureOptions());
        IConnectionProtocolServer[] playing = [http, http, ftp, ftp, gopher, gopher, mqtt, mqtt, new DictProtocolServer(noDefinitions), new TelnetProtocolServer(), ssh, smb,
            new ImapUpstreamTestCaseServer(mailReplies, new AnonymousAuthenticationPolicy()),
            new Pop3UpstreamTestCaseServer(mailReplies, new AnonymousAuthenticationPolicy()),
            new SmtpUpstreamTestCaseServer(mailReplies, new AnonymousAuthenticationPolicy()), proxy, proxy, socks, socks];
        for (var index = 0; index < servers.Length; index++)
        {
            servers[index].Current = arrivalLog.Wrap(playing[index]);
        }

        tftp.Current = arrivalLog.Wrap(new TftpUpstreamTestCaseServer(tftpReplies));
        dns.Current = arrivalLog.Wrap(new DnsUpstreamTestCaseServer(dnsReplies));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        await serving;
        stop.Dispose();
    }

    private static string PortKey(string scheme, string host) => $"{scheme} {host}";

    private static ListenUrl ListenUrlFor(string scheme, string host) => scheme switch
    {
        HttpProxyScheme => new ListenUrl(scheme, host, 0) { IsImplicitTls = false },
        HttpsProxyScheme => new ListenUrl(scheme, host, 0) { IsProxy = true },
        _ => new ListenUrl(scheme, host, 0),
    };

    // The HTTP listener on a Unix socket shares the http scheme, so the plaintext HTTP case server
    // answers it; its host only keys its row apart from the TCP listener's, as nothing binds it.
    private static ListenUrl[] HttpUnixSocketListenUrls(string? path) =>
        path is null ? [] : [new ListenUrl("http", "localhost", 0) { UnixSocket = new UnixSocketAddress(path, isAbstract: false) }];

    // The SOCKS5 listener on a Unix socket shares the socks5 scheme, so the SOCKS case server answers it.
    private static ListenUrl[] SocksUnixSocketListenUrls(string? path) =>
        path is null ? [] : [new ListenUrl("socks5", "localhost", 0) { UnixSocket = new UnixSocketAddress(path, isAbstract: false) }];

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
    /// Registers one datagram scheme for whichever case server <see cref="Play"/> set.
    /// </summary>
    private sealed class CaseDatagramServer(string scheme) : IDatagramProtocolServer
    {
        public volatile IDatagramProtocolServer? Current;

        public IReadOnlyList<string> Schemes { get; } = [scheme];

        public Task ServeAsync(IDatagramFlow flow, ExchangeContext context) =>
            Current?.ServeAsync(flow, context) ?? Task.CompletedTask;
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
            Record(listenUrl, listener.ListenUrl);
            return listener;
        }

        public async ValueTask<IDatagramListener> StartDatagramListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken)
        {
            var listener = await inner.StartDatagramListenerAsync(listenUrl, cancellationToken);
            Record(listenUrl, listener.ListenUrl);
            return listener;
        }

        private void Record(ListenUrl asked, ListenUrl bound)
        {
            lock (ports)
            {
                ports[PortKey(asked.Scheme, asked.Host)] = bound.BoundPort ?? bound.Port;
                if (ports.Count == expectedListenerCount)
                {
                    allBound.SetResult(new Dictionary<string, int>(ports, StringComparer.Ordinal));
                }
            }
        }
    }

    /// <summary>
    /// Keeps the notes every exchange writes, for the test log; bytes are not kept twice.
    /// </summary>
    private sealed class NoteCollectingExchangeLogFactory : IExchangeLogFactory
    {
        private readonly StringBuilder notes = new();
        private int openExchanges;

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

        public bool HasOpenExchanges => Volatile.Read(ref openExchanges) > 0;

        public IExchangeLog Create(long exchangeId, EndPoint remoteEndPoint)
        {
            Interlocked.Increment(ref openExchanges);
            return new Log(this, exchangeId);
        }

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

            public void Note(string text)
            {
                owner.Append($"* [{exchangeId}] {text}");
                if (text.StartsWith($"Exchange {exchangeId} ended;", StringComparison.Ordinal))
                {
                    Interlocked.Decrement(ref owner.openExchanges);
                }
            }
        }
    }
}
